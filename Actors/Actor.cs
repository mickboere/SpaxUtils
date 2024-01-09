using System;
using System.Linq;
using System.Collections.Generic;

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
		public const float DEFAULT_RETRY_WINDOW = 0.333f;

		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		public int Priority => int.MaxValue;
		public List<string> SupportsActs { get; private set; } = new List<string>();
		public bool Performing => MainPerformer == null ? false : MainPerformer.Performing;
		public bool Finishing => MainPerformer == null ? false : MainPerformer.Finishing;
		public bool Completed => MainPerformer == null ? false : MainPerformer.Completed;
		public float RunTime => MainPerformer == null ? 0f : MainPerformer.RunTime;

		public IPerformer MainPerformer => activePerformers.Count > 0 ? activePerformers[activePerformers.Count - 1] : null;
		public float RetryWindow { get; set; }

		private bool autoRefreshSupport;
		private List<IPerformer> availablePerformers;
		private List<IPerformer> activePerformers = new List<IPerformer>();
		private Act<bool>? lastAct;
		private (Act<bool> act, Timer timer)? lastFailedAttempt;

		public Actor(
			string identifier = DEFAULT_IDENTIFIER,
			float retryWindow = DEFAULT_RETRY_WINDOW,
			IEnumerable<IPerformer> performers = null)
				: base(identifier)
		{
			RetryWindow = retryWindow;

			// Collect performers.
			this.availablePerformers = new List<IPerformer>();
			if (performers != null)
			{
				autoRefreshSupport = false;
				foreach (IPerformer performer in performers)
				{
					AddPerformer(performer);
				}
			}

			autoRefreshSupport = true;
			RefreshSupport();
		}

		public void Dispose()
		{
			autoRefreshSupport = false;
			while (availablePerformers.Count > 0)
			{
				RemovePerformer(availablePerformers[0]);
			}
		}

		/// <inheritdoc/>
		public void Send<T>(T act, Timer timer = default) where T : IAct
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
			availablePerformers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

			performer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent += OnPerformanceCompletedEvent;

			if (autoRefreshSupport)
			{
				RefreshSupport();
			}
		}

		/// <inheritdoc/>
		public void RemovePerformer(IPerformer performer)
		{
			if (!availablePerformers.Contains(performer))
			{
				return;
			}

			availablePerformers.Remove(performer);
			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;

			if (autoRefreshSupport)
			{
				RefreshSupport();
			}
		}
		#endregion Management

		#region Production
		/// <inheritdoc/>
		public bool TryProduce(IAct act, out IPerformer finalPerformer)
		{
			finalPerformer = null;
			if (!SupportsActs.Contains(act.Title) ||
				(MainPerformer != null && !MainPerformer.Finishing))
			{
				return false;
			}

			foreach (IPerformer performer in availablePerformers)
			{
				if (performer.SupportsActs.Contains(act.Title))
				{
					if (performer.TryProduce(act, out finalPerformer))
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <inheritdoc/>
		protected override void OnReceived(string key, IAct value)
		{
			base.OnReceived(key, value);

			if (!SupportsActs.Contains(value.Title))
			{
				// Act type not supported.
				return;
			}

			// Default Act<bool> behaviour (acting on button input for example)
			if (value is Act<bool> boolAct)
			{
				// Ensure input is allowed during current activity.
				if (lastAct.HasValue && lastAct.Value.Value && lastAct.Value.Title != boolAct.Title)
				{
					// Invalid input for current performance, a <false> value is required first.
					return;
				}

				bool failed = false;

				// Try produce Act.
				if (boolAct.Value)
				{
					if (TryProduce(boolAct, out IPerformer performer) && !activePerformers.Contains(performer))
					{
						activePerformers.Add(performer);
					}
					else if (MainPerformer.Performing)
					{
						failed = true;
					}
				}
				// Try perform Act.
				else if (MainPerformer != null && !MainPerformer.TryPerform())
				{
					failed = true;
				}

				lastFailedAttempt = failed ? (boolAct, new Timer(RetryWindow)) : null;
				lastAct = boolAct;
			}
		}
		#endregion Production

		#region Performance
		/// <inheritdoc/>
		public bool TryPerform()
		{
			return MainPerformer == null ? false : MainPerformer.TryPerform();
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
			PerformanceCompletedEvent?.Invoke(performer);

			activePerformers.Remove(performer);
		}
		#endregion Performance

		private void RetryLastFailedAttempt()
		{
			// If there was a failed action attempt within the last retryActionWindow OR of positive value, retry it.
			if (lastFailedAttempt.HasValue && (lastFailedAttempt.Value.act.Value || !lastFailedAttempt.Value.timer.Expired))
			{
				// If the last attempt was positive, only redo the positive as the input still needs to be released manually.
				// If the last attempt was negative, redo both positive and negative input to do a full performance.
				Act<bool> retry = lastFailedAttempt.Value.act;
				OnReceived(retry.Title, new Act<bool>(retry.Title, true));
				if (!retry.Value)
				{
					OnReceived(retry.Title, new Act<bool>(retry.Title, false));
				}
			}
		}

		private void RefreshSupport()
		{
			SupportsActs = new List<string>();
			foreach (IPerformer performer in availablePerformers)
			{
				SupportsActs.AddRange(performer.SupportsActs);
			}
		}
	}
}
