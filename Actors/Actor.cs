using System;
using System.Linq;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Class that manages the desired actions of an Agent in the form of <see cref="IAct"/>s.
	/// A single <see cref="IPerformer"/> can be started to take control of the Agent and fulfil an <see cref="IAct"/>.
	/// </summary>
	public class Actor : ChannelBase<string, IAct>, IActor, IDisposable
	{
		public const string DEFAULT_ACTOR_IDENTIFIER = "ACTOR";

		public event IPerformer.PerformanceUpdateDelegate PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		public int Priority => 0;
		public List<string> SupportsActs { get; private set; } = new List<string>();
		public float PerformanceTime => current == null ? 0f : current.PerformanceTime;
		public bool Performing => current != null && current.Performing;
		public bool Completed => current == null || current.Completed;

		private List<IPerformer> performers;
		private IPerformer current;
		private bool autoRefreshSupport;

		public Actor(string identifier = DEFAULT_ACTOR_IDENTIFIER, IEnumerable<IPerformer> performers = null) : base(identifier)
		{
			this.performers = new List<IPerformer>();
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
			while (performers.Count > 0)
			{
				RemovePerformer(performers[0]);
			}
		}

		/// <inheritdoc/>
		public void Send<T>(T act, Timer timer = default) where T : IAct
		{
			base.Send<T>(act.Title, act, timer);
		}

		/// <inheritdoc/>
		public void AddPerformer(IPerformer performer)
		{
			if (performers.Contains(performer))
			{
				return;
			}

			if (performer == this)
			{
				SpaxDebug.Error("Cannot add an Actor as one of its own performers!");
				return;
			}

			performers.Add(performer);
			performers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

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
			if (!performers.Contains(performer))
			{
				return;
			}

			performers.Remove(performer);
			performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			performer.PerformanceCompletedEvent -= OnPerformanceCompletedEvent;

			if (autoRefreshSupport)
			{
				RefreshSupport();
			}
		}

		/// <inheritdoc/>
		public bool TryProduce(IAct act, out IPerformer finalPerformer)
		{
			finalPerformer = null;
			if (Performing || !SupportsActs.Contains(act.Title))
			{
				return false;
			}

			foreach (IPerformer performer in performers)
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
		public bool TryPerform()
		{
			if (current == null)
			{
				return false;
			}

			return current.TryPerform();
		}

		private void RefreshSupport()
		{
			SupportsActs = new List<string>();
			foreach (IPerformer performer in performers)
			{
				SupportsActs.AddRange(performer.SupportsActs);
			}
		}

		private void OnPerformanceUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			if (current == null)
			{
				current = performer;
			}
			PerformanceUpdateEvent?.Invoke(performer, pose, weight);
		}

		private void OnPerformanceCompletedEvent(IPerformer performer)
		{
			if (current == performer)
			{
				current = null;
			}
			PerformanceCompletedEvent?.Invoke(performer);
		}
	}
}
