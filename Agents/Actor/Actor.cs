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
		public event Action<IPerformer> PerformanceStartedEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		/// <inheritdoc/>
		public IPerformer MainPerformer => activePerformers.Count > 0 ? activePerformers[activePerformers.Count - 1] : null;

		/// <inheritdoc/>
		public bool Blocked => blockers.Count > 0;

		/// <inheritdoc/>
		public int Priority => int.MaxValue;

		/// <inheritdoc/>
		public IAct Act => MainPerformer != null ? MainPerformer.Act : null;

		/// <inheritdoc/>
		public PerformanceState State => MainPerformer != null ? MainPerformer.State : PerformanceState.Inactive;

		/// <inheritdoc/>
		public float RunTime => MainPerformer != null ? MainPerformer.RunTime : 0f;

		private CallbackService callbackService;
		private Dictionary<string, InputToActMapper> inputMappers = new Dictionary<string, InputToActMapper>();
		private List<IPerformer> availablePerformers = new List<IPerformer>();
		private List<IPerformer> activePerformers = new List<IPerformer>();
		private Act<bool>? lastPerformedInput;
		private (Act<bool> act, TimerStruct timer)? lastFailedAttempt;
		private List<object> blockers = new List<object>();

		public Actor(string identifier, CallbackService callbackService, InputToActMap inputToActMap = null,
			IEnumerable<IPerformer> performers = null) : base(identifier)
		{
			this.callbackService = callbackService;
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);

			// Initialize input to act mappers.
			if (inputToActMap != null)
			{
				foreach (InputToActMapping mapping in inputToActMap.Mappings)
				{
					inputMappers.Add(mapping.Title, new InputToActMapper(this, mapping));
				}
			}

			// Register performers.
			if (performers != null)
			{
				foreach (IPerformer performer in performers)
				{
					if (performer != this)
					{
						AddPerformer(performer);
					}
				}
			}
		}

		public void Dispose()
		{
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
			while (availablePerformers.Count > 0)
			{
				RemovePerformer(availablePerformers[0]);
			}
			foreach (InputToActMapper mapper in inputMappers.Values)
			{
				mapper.Dispose();
			}
		}

		protected void OnUpdate(float delta)
		{
			foreach (InputToActMapper mapper in inputMappers.Values)
			{
				mapper.Update();
			}
		}

		/// <inheritdoc/>
		public void Send<T>(T act, TimerStruct timer = default) where T : IAct
		{
			base.Send<T>(act.Title, act, timer);
		}

		/// <inheritdoc/>
		public void SendInput(string act, bool? value = null)
		{
			if (inputMappers.ContainsKey(act))
			{
				if (value.HasValue)
				{
					inputMappers[act].Send(value.Value);
				}
				else
				{
					inputMappers[act].Send(true);
					inputMappers[act].Send(false);
				}
			}
			else
			{
				if (value.HasValue)
				{
					Send(new Act<bool>(act, value.Value));
				}
				else
				{
					Send(new Act<bool>(act, true));
					Send(new Act<bool>(act, false));
				}
			}
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
			if (act is Act<bool> input)
			{
				if (MainPerformer != null && Act.Title == act.Title &&
					input.Value && State is PerformanceState.Preparing)
				{
					// Don't process continuous input.
					return;
				}

				if ((input.Value && TryPrepare(input, out _)) ||
					(!input.Value && MainPerformer != null && lastPerformedInput.HasValue &&
						lastPerformedInput.Value.Title == input.Title && lastPerformedInput.Value.Value &&
						MainPerformer.TryPerform()))
				{
					lastPerformedInput = act.Title == ActorActs.CANCEL ? null : input;
					lastFailedAttempt = null;
				}
				else
				{
					lastFailedAttempt = (input, new TimerStruct(act.Buffer));
				}
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

		#region Blocking

		/// <inheritdoc/>
		public void AddBlocker(object blocker)
		{
			if (!blockers.Contains(blocker))
			{
				blockers.Add(blocker);
			}
		}

		/// <inheritdoc/>
		public void RemoveBlocker(object blocker)
		{
			if (blockers.Contains(blocker))
			{
				blockers.Remove(blocker);
			}
		}

		#endregion Blocking

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
