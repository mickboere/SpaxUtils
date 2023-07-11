using SpaxUtils.StateMachine;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface that describes an agent setup with identification, frame, body, data and behaviour.
	/// </summary>
	public interface IAgentSetup
	{
		/// <summary>
		/// The identification of this agent.
		/// </summary>
		IIdentification Identification { get; }

		/// <summary>
		/// The root agent prefab containing the components shared between all agents.
		/// </summary>
		Agent Frame { get; }

		/// <summary>
		/// The main rig object containing the <see cref="Animator"/>, if any.
		/// </summary>
		AgentBodyComponent Body { get; }

		/// <summary>
		/// The <see cref="StateMachineGraph"/>s to append to the brain.
		/// The Brain decides an Agent's behavior.
		/// By linking states and adding specific components to them, a refined behavioral tree can be created.
		/// </summary>
		IList<StateMachineGraph> BrainGraphs { get; }

		/// <summary>
		/// All objects that should be instantiated as a child of the agent.
		/// Can contain additional visuals, entity component prefabs, etc.
		/// </summary>
		IList<GameObject> Children { get; }

		/// <summary>
		/// All additional dependencies that should be added to the dependency injector.
		/// Used to define loaded data or configurations for components.
		/// </summary>
		IList<object> Dependencies { get; }
	}
}
