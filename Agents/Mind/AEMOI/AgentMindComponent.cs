using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that IS the agent's <see cref="IMind"/> (AEMOI).
	/// Bound as a MonoBehaviour during dependency injection; Agent receives it via
	/// <c>[Optional] IMind mind</c> — same pattern as Targeter/Targetable.
	/// Internally owns and delegates to an <see cref="AEMOI"/> instance.
	///
	/// Personality and inclination resolve in priority order:
	///   1. Explicit <see cref="Vector8ConfigurationAsset"/> bound under the mind data keys.
	///   2. Body/soul distribution from <see cref="AgentStatHandler"/>, calculated once at init.
	///   3. <see cref="Vector8.Half"/> (neutral fallback).
	/// </summary>
	public class AgentMindComponent : AgentComponentBase, IMind
	{
		#region IMind

		#region Events

		public event Action ActivatedEvent { add => aemoi.ActivatedEvent += value; remove => aemoi.ActivatedEvent -= value; }
		public event Action DeactivatedEvent { add => aemoi.DeactivatedEvent += value; remove => aemoi.DeactivatedEvent -= value; }
		public event Action<float> UpdatingEvent { add => aemoi.UpdatingEvent += value; remove => aemoi.UpdatingEvent -= value; }
		public event Action MotivatedEvent { add => aemoi.MotivatedEvent += value; remove => aemoi.MotivatedEvent -= value; }
		public event Action UpdatedEvent { add => aemoi.UpdatedEvent += value; remove => aemoi.UpdatedEvent -= value; }

		#endregion Events

		#region Properties

		public bool Active => aemoi.Active;
		public Vector8 Inclination => aemoi.Inclination;
		public Vector8 Personality => aemoi.Personality;
		public IReadOnlyDictionary<IEntity, Vector8> Stimuli => aemoi.Stimuli;
		public (Vector8 emotion, IEntity target) Motivation => aemoi.Motivation;
		public IMindBehaviour ActiveBehaviour => aemoi.ActiveBehaviour;
		public IEntity ActiveTarget => aemoi.ActiveTarget;
		public Vector8 Emotion => aemoi.Emotion;
		public Vector8 Balance => aemoi.Balance;

		#endregion Properties

		#region Methods

		public void Activate(bool reset) => aemoi.Activate(reset);
		public void Deactivate() => aemoi.Deactivate();
		void IMind.Update(float delta) => aemoi.Update(delta);
		public Vector8 RetrieveStimuli(IEntity source) => aemoi.RetrieveStimuli(source);
		public void Stimulate(Vector8 stim, IEntity source) => aemoi.Stimulate(stim, source);
		public void Satisfy(Vector8 sat, IEntity source) => aemoi.Satisfy(sat, source);
		public void SetFilter(IEntity entity, Vector8 filter) => aemoi.SetFilter(entity, filter);
		public void RemoveFilter(IEntity entity) => aemoi.RemoveFilter(entity);
		public void AddBehaviour(IMindBehaviour b) => aemoi.AddBehaviour(b);
		public void AddBehaviours(IEnumerable<IMindBehaviour> bs) => aemoi.AddBehaviours(bs);
		public void RemoveBehaviour(IMindBehaviour b) => aemoi.RemoveBehaviour(b);
		public void RemoveBehaviours(IEnumerable<IMindBehaviour> bs) => aemoi.RemoveBehaviours(bs);
		public void Dispose() => aemoi?.Dispose();

		#endregion Methods

		#endregion IMind

		private AEMOI aemoi;

		public void InjectDependencies(
			IAgent agent,
			AEMOISettings settings,
			[Optional, BindingIdentifier(MindDataIdentifiers.PERSONALITY)] Vector8 personality,
			[Optional, BindingIdentifier(MindDataIdentifiers.INCLINATION)] Vector8 inclination,
			[Optional] AgentStatHandler statHandler,
			[Optional] AEMOIBehaviourAsset[] behaviour)
		{
			float difficulty = agent.RuntimeData.GetValue(EntityDataIdentifiers.DIFFICULTY, 0.5f);

			Vector8 resolvedPersonality = personality != Vector8.Zero ? personality
				: statHandler != null ? statHandler.BodyDistribution * Mathf.Clamp01(difficulty)
				: Vector8.Half;
			Vector8 resolvedInclination = inclination != Vector8.Zero ? inclination
				: statHandler != null ? statHandler.SoulDistribution
				: Vector8.Half;

			aemoi = new AEMOI(agent.DependencyManager, settings,
				new StatOctad(agent, settings.Inclination, resolvedInclination),
				new StatOctad(agent, settings.Personality, resolvedPersonality));

			if (behaviour != null)
			{
				foreach (AEMOIBehaviourAsset asset in behaviour)
				{
					IMindBehaviour b = (IMindBehaviour)asset.CreateInstance();
					agent.DependencyManager.Inject(b);
					aemoi.AddBehaviour(b);
				}
			}
		}
	}
}
