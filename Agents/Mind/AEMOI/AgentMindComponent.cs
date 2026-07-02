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
	/// Personality and inclination resolve (lazily, on first <see cref="Activate"/>) in priority order:
	///   1. Explicit <see cref="Vector8ConfigurationAsset"/> bound under the mind data keys.
	///   2. The agent's stat distributions (<see cref="AgentStatHandler"/> body → personality, soul → inclination),
	///      remapped toward each trait's floor by difficulty.
	///   3. <see cref="Vector8.Half"/> (neutral fallback).
	/// </summary>
	public class AgentMindComponent : AgentComponentBase, IMind
	{
		#region IMind Properties

		#region Events

		public event Action ActivatedEvent { add => aemoi.ActivatedEvent += value; remove => aemoi.ActivatedEvent -= value; }
		public event Action DeactivatedEvent { add => aemoi.DeactivatedEvent += value; remove => aemoi.DeactivatedEvent -= value; }
		public event Action<float> UpdatingEvent { add => aemoi.UpdatingEvent += value; remove => aemoi.UpdatingEvent -= value; }
		public event Action MotivatedEvent { add => aemoi.MotivatedEvent += value; remove => aemoi.MotivatedEvent -= value; }
		public event Action UpdatedEvent { add => aemoi.UpdatedEvent += value; remove => aemoi.UpdatedEvent -= value; }

		#endregion Events

		public bool Active => aemoi.Active;
		public Vector8 Inclination => aemoi.Inclination;
		public Vector8 Personality => aemoi.Personality;
		public IReadOnlyDictionary<IEntity, Vector8> Stimuli => aemoi.Stimuli;
		public (Vector8 emotion, IEntity target) Motivation => aemoi.Motivation;
		public IMindBehaviour ActiveBehaviour => aemoi.ActiveBehaviour;
		public IEntity ActiveTarget => aemoi.ActiveTarget;
		public Vector8 Emotion => aemoi.Emotion;
		public Vector8 EmotionNormalized => aemoi.EmotionNormalized;
		public Vector8 Balance => aemoi.Balance;

		#endregion IMind Properties

		private AEMOI aemoi;
		private AEMOISettings settings;
		private AgentStatHandler statHandler;
		private Vector8 explicitPersonality;
		private Vector8 explicitInclination;
		private bool traitsResolved;

		public void InjectDependencies(
			IAgent agent,
			AEMOISettings settings,
			[Optional, BindingIdentifier(MindDataIdentifiers.PERSONALITY)] Vector8 personality,
			[Optional, BindingIdentifier(MindDataIdentifiers.INCLINATION)] Vector8 inclination,
			[Optional] AgentStatHandler statHandler,
			[Optional] AEMOIBehaviourAsset[] behaviour)
		{
			base.InjectDependencies(agent);
			this.settings = settings;
			this.statHandler = statHandler;
			explicitPersonality = personality;
			explicitInclination = inclination;

			// Traits are resolved lazily in Activate(), NOT here: they derive from the agent's stat distributions
			// (AgentStatHandler.Body/SoulDistribution), which are only computed in that handler's Awake/InitializeStats —
			// which runs AFTER every InjectDependencies. Reading them now would just read default(Vector8). The mind is
			// built neutral and gets its real traits the moment the brain activates it (guaranteed post-stat-init).
			aemoi = new AEMOI(agent.DependencyManager, settings, Vector8.Half, Vector8.Half);

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

		#region IMind Methods

		public void Activate(bool reset)
		{
			ResolveTraits();
			aemoi.Activate(reset);
		}

		public void Deactivate() => aemoi.Deactivate();
		void IMind.Update(float delta) => aemoi.Update(delta);
		public Vector8 RetrieveStimuli(IEntity source) => aemoi.RetrieveStimuli(source);
		public Vector8 RetrieveDemand(IEntity source) => aemoi.RetrieveDemand(source);
		public void SetDemand(Vector8 demand, IEntity source) => aemoi.SetDemand(demand, source);
		public void Stimulate(Vector8 stim, IEntity source) => aemoi.Stimulate(stim, source);
		public void Satisfy(Vector8 sat, IEntity source) => aemoi.Satisfy(sat, source);
		public void ClearStimuli(IEntity source) => aemoi.ClearStimuli(source);
		public void AddBehaviour(IMindBehaviour b) => aemoi.AddBehaviour(b);
		public void AddBehaviours(IEnumerable<IMindBehaviour> bs) => aemoi.AddBehaviours(bs);
		public void RemoveBehaviour(IMindBehaviour b) => aemoi.RemoveBehaviour(b);
		public void RemoveBehaviours(IEnumerable<IMindBehaviour> bs) => aemoi.RemoveBehaviours(bs);
		public void Dispose() => aemoi?.Dispose();

		#endregion IMind Methods

		/// <summary>
		/// Resolves Personality/Inclination once, on first activation — by which point the agent's stat distributions
		/// are initialized. Priority: explicit <c>MIND/*</c> binding → stat distribution remapped by difficulty → neutral.
		/// Personality derives from the body distribution, Inclination from the soul distribution.
		/// </summary>
		private void ResolveTraits()
		{
			if (traitsResolved)
			{
				return;
			}
			traitsResolved = true;

			float difficulty = Agent.RuntimeData.GetValue(EntityDataIdentifiers.DIFFICULTY, 0.5f);
			Vector8 bodyDist = statHandler != null ? statHandler.BodyDistribution : Vector8.Zero;
			Vector8 soulDist = statHandler != null ? statHandler.SoulDistribution : Vector8.Zero;

			// Difficulty (cognitive only, never combat stats) remaps each trait toward its floor as it drops — its only
			// handle; the remapped inclination then drives the AEMOI rates. An explicit MIND binding still wins.
			Vector8 resolvedPersonality = explicitPersonality != Vector8.Zero ? explicitPersonality
				: bodyDist != Vector8.Zero ? RemapToFloor(bodyDist, settings.PersonalityFloor, difficulty)
				: Vector8.Half;
			Vector8 resolvedInclination = explicitInclination != Vector8.Zero ? explicitInclination
				: soulDist != Vector8.Zero ? RemapToFloor(soulDist, settings.InclinationFloor, difficulty)
				: Vector8.Half;

			aemoi.SetTraits(resolvedInclination, resolvedPersonality);
		}

		/// <summary>Remaps a [0,1] distribution onto [floor, floor+(1-floor)·difficulty], preserving shape:
		/// full trait at difficulty 1, collapsing toward the floor as difficulty drops.</summary>
		private static Vector8 RemapToFloor(Vector8 distribution, float floor, float difficulty)
		{
			return Vector8.One * floor + distribution * ((1f - floor) * Mathf.Clamp01(difficulty));
		}
	}
}
