using System;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that transmits reported impacts both locally through the <see cref="ICommunicationChannel"/>
	/// and globally through the <see cref="GlobalImpactService"/>.
	/// Will also listen for any impacts it was a victim of and pass them on locally.
	/// </summary>
	public class AgentImpactHandler : AgentComponentBase
	{
		public event Action<ImpactData> ImpactEvent;

		private GlobalImpactService impactService;
		private ICommunicationChannel comms;

		public void InjectDependencies(GlobalImpactService impactService, ICommunicationChannel comms)
		{
			this.impactService = impactService;
			this.comms = comms;
		}

		protected void OnEnable()
		{
			impactService.AddListener(Entity, OnMentioned);
		}

		protected void OnDisable()
		{
			impactService.RemoveListener(Entity);
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
			impactService.ReportImpact(impact);
		}
	}
}
