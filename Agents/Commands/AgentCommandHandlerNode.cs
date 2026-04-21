using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Brain node that listens for <see cref="AgentCommandMsg"/> on the agent's communication channel
	/// and executes the corresponding command. Intended for the Cutscene brain state but can be placed
	/// on any layer where external command dispatch is desired.
	/// </summary>
	public class AgentCommandHandlerNode : StateMachineNodeBase
	{
		[SerializeField, NodeInput] protected Connections.StateComponent inConnection;

		[Header("Defaults")]
		[SerializeField] private float moveSpeed = 0.5f;
		[SerializeField] private float arrivalRange = 0.25f;
		[SerializeField, Tooltip("If true, vacates any occupied POI when this state is exited.")] private bool vacateOnExit = true;

		private IAgent agent;
		private AgentNavigationHandler navigation;
		private EntityService entityService;
		private POIHandler poiHandler;
		private CallbackService callbackService;

		private POIVisitHelper visitHelper;

		public void InjectDependencies(
			IAgent agent,
			AgentNavigationHandler navigation,
			EntityService entityService,
			POIHandler poiHandler,
			CallbackService callbackService)
		{
			this.agent = agent;
			this.navigation = navigation;
			this.entityService = entityService;
			this.poiHandler = poiHandler;
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			agent.Comms.Listen<AgentCommandMsg>(this, OnCommandReceived);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			agent.Comms.StopListening(this);
			DisposeHelper();

			if (vacateOnExit && poiHandler.IsOccupying)
			{
				poiHandler.Vacate();
			}
		}

		private void OnCommandReceived(AgentCommandMsg msg)
		{
			switch (msg.Command)
			{
				case AgentCommand.OccupyPOI:
					DisposeHelper();
					visitHelper = new POIVisitHelper(agent, navigation, entityService, poiHandler, callbackService);
					visitHelper.Visit(msg.Parameter, msg.Immediate, moveSpeed, arrivalRange);
					break;

				case AgentCommand.VacatePOI:
					DisposeHelper();
					if (poiHandler.IsOccupying)
					{
						poiHandler.Vacate();
					}
					break;
			}
		}

		private void DisposeHelper()
		{
			if (visitHelper != null)
			{
				visitHelper.Dispose();
				visitHelper = null;
			}
		}
	}
}
