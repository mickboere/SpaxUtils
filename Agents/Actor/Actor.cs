using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that manages the desired actions of an Agent in the form of <see cref="IAct"/>s.
	/// A single <see cref="IPerformer"/> can be started to take control of the Agent and fulfil an <see cref="IAct"/>.
	/// <see cref="IActor"/> implements <see cref="IPerformer"/> and thus can be treated as such.
	/// </summary>
	public class Actor : ChannelBase<string, IAct>, IActor, IDisposable
	{
		public const string DEFAULT_IDENTIFIER = "ACTOR";

		public event Action<IPerformer> PerformanceStartedEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		public int Priority => int.MaxValue;
		public IAct Act => MainPerformer != null ? MainPerformer.Act : null;
		public PerformanceState State => MainPerformer != null ? MainPerformer.State : PerformanceState.Inactive;
		public float RunTime => MainPerformer != null ? MainPerformer.RunTime : 0f;
		public bool Blocked { get; set; } = false;

		/// <summary>
		/// The last performer to be activated.
		/// </summary>
		public IPerformer MainPerformer => activePerformers.Count > 0 ? activePerformers[activePerformers.Count - 1] : null;

		private List<IPerformer> availablePerformers;
		private List<IPerformer> activePerformers = new List<IPerformer>();
		private Act<bool>? lastPerformedAct;
		private (Act<bool> act, TimerStruct timer)? lastFailedAttempt;

		public Actor(
			string identifier = DEFAULT_IDENTIFIER,
			IEnumerable<IPerformer> performers = null)
				: base(identifier)
		{
			// Collect performers.
			this.availablePerformers = new List<IPerformer>();
			if (performers != null)
			{
				foreach (IPerformer performer in performers)
				{
					AddPerformer(performer);
				}
			}
		}

		public void Dispose()
		{
			while (availablePerformers.Count > 0)
			{
				RemovePerformer(availablePerformers[0]);
			}
		}

		/// <inheritdoc/>
		public void Send<T>(T act, TimerStruct timer = default) where T : IAct
		{
			base.Send<T>(act.Title, act, timer);
		}

		#region Management

		/// <inheritdoc/>
		public void AddPerformer(IPerformer performer)
		{
			if (availablePerformers.Contains(performer))
			{
				return;
			}

			if (performer == this)
			{
				SpaxDebug.Error("Cannot add an Actor as one of its own performers!");
				return;
			}

			availablePerformers.Add(performer);
			if (availablePerformers.Count > 2 && performer.Priority > availablePerformers[availablePerformers.Count - 2].Priority)
			{
				availablePerformers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
			}

			performer.PerformanceStartedEvent += OnPerformanceStartedEvent;
			performer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent += OnPerformanceCompletedEvent;
		}

		/// <inheritdoc/>
		public void RemovePerformer(IPerformer performer)
		{
			if (!availablePerformers.Contains(performer))
			{
				return;
			}

			availablePerformers.Remove(performer);
			performer.PerformanceStartedEvent -= OnPerformanceStartedEvent;
			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;
		}

		#endregion Management

		#region Production

		/// <inheritdoc/>
		protected override void OnReceived(string key, IAct act)
		{
			base.OnReceived(key, act);

			if (!SupportsAct(act.Title) || Blocked)
			{
				return;
			}

			// Default Act<bool> behaviour
			// (AKA button behaviour - TRUE prepares act, FALSE performs it).
			if (act is Act<bool> boolAct)
			{
				if (boolAct.Value && MainPerformer != null && Act.Title == act.Title && !(State.HasFlag(PerformanceState.Finishing) || State.HasFlag(PerformanceState.Completed)))
				{
					// Don't process duplicate / continuous input.
					return;
				}

				bool failed = true;
				if ((boolAct.Value && TryPrepare(boolAct, out _)) ||
					(!boolAct.Value && MainPerformer != null && lastPerformedAct.HasValue &&
						lastPerformedAct.Value.Title == boolAct.Title && lastPerformedAct.Value.Value &&
						MainPerformer.TryPerform()))
				{
					failed = false;
					lastPerformedAct = act.Title == ActorActs.CANCEL ? null : boolAct;
				}

				lastFailedAttempt = failed ? (boolAct, new TimerStruct(act.Buffer)) : null;
			}
		}

		/// <inheritdoc/>
		public bool SupportsAct(string act)
		{
			if (act == ActorActs.CANCEL)
			{
				return true;
			}

			foreach (IPerformer performer in availablePerformers)
			{
				if (performer.SupportsAct(act))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc/>
		public bool TryPrepare(IAct act, out IPerformer finalPerformer)
		{
			finalPerformer = null;

			// Soft-Cancel mechanic.
			if (MainPerformer != null && act.Title == ActorActs.CANCEL)
			{
				finalPerformer = MainPerformer;
				return MainPerformer.TryCancel(false);
			}

			// Ensure Support and Non-Occupance or Interuptability.
			if (!Blocked && SupportsAct(act.Title) && (MainPerformer == null ||
				State == PerformanceState.Finishing || State == PerformanceState.Completed ||
				(act.Interuptor && Act.Interuptable && TryCancel(false))))
			{
				// Try start new performance.
				foreach (IPerformer performer in availablePerformers)
				{
					if (performer.SupportsAct(act.Title) &&
						performer.TryPrepare(act, out finalPerformer))
					{
						activePerformers.Add(finalPerformer);
						return true;
					}
				}
			}

			return false;
		}

		#endregion Production

		#region Performance

		/// <inheritdoc/>
		public bool TryPerform()
		{
			return MainPerformer == null ? false : MainPerformer.TryPerform();
		}

		/// <inheritdoc/>
		public bool TryCancel(bool force)
		{
			return MainPerformer == null ? false : MainPerformer.TryCancel(force);
		}

		private void OnPerformanceStartedEvent(IPerformer performer)
		{
			PerformanceStartedEvent?.Invoke(performer);
		}

		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			PerformanceUpdateEvent?.Invoke(performer);

			if (performer == MainPerformer)
			{
				RetryLastFailedAttempt();
			}
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			activePerformers.Remove(performer);
			PerformanceCompletedEvent?.Invoke(performer);
		}

		#endregion Performance

		private void RetryLastFailedAttempt()
		{
			// If there was a failed action attempt previous frame or earlier AND its buffer timer hasn't expired yet OR the input button is down, retry it.
			if (lastFailedAttempt.HasValue &&
				lastFailedAttempt.Value.timer.StartTime < lastFailedAttempt.Value.timer.CurrentTime &&
				(lastFailedAttempt.Value.act.Value || !lastFailedAttempt.Value.timer.Expired))
			{
				// If the last attempt was positive, only redo the positive as the input still needs to be released manually.
				// If the last attempt was negative, redo both positive and negative input to simulate a full button press.
				Act<bool> retry = lastFailedAttempt.Value.act;
				float buffer = lastFailedAttempt.Value.timer.Remaining;
				OnReceived(retry.Title, new Act<bool>(retry.Title, true, retry.Interuptable, retry.Interuptor, buffer));
				if (!retry.Value)
				{
					OnReceived(retry.Title, new Act<bool>(retry.Title, false, retry.Interuptable, retry.Interuptor, buffer));
				}
			}
		}
	}
}
