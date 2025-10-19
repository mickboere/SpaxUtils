using System;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that transmits reported impacts both locally through the <see cref="ICommunicationChannel"/>
	/// and globally through the <see cref="AwarenessService"/>.
	/// Will also listen for any impacts it was a victim of and transmit them locally.
	/// </summary>
	public class AwarenessComponent : AgentComponentBase
	{
		public event Action<ImpactData> ImpactEvent;

		private AwarenessService awarenessService;
		private ICommunicationChannel comms;

		public void InjectDependencies(AwarenessService awarenessService, ICommunicationChannel comms)
		{
			this.awarenessService = awarenessService;
			this.comms = comms;
		}

		protected void OnEnable()
		{
			awarenessService.AddListener(Entity, OnMentioned);
		}

		protected void OnDisable()
		{
			awarenessService.RemoveListener(Entity);
		}

		private void OnMentioned(ImpactData impact)
		{
			ImpactEvent?.Invoke(impact);
			comms.Send(impact);
		}

		public void ReportImpact(ImpactData impact)
		{
			ImpactEvent?.Invoke(impact);
			comms.Send(impact);
			awarenessService.ReportImpact(impact);
		}
	}
}
