using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that adjust the Agent's stats while guarding.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_Deflecting", menuName = "ScriptableObjects/Combat/DeflectingCombatBehaviourAsset")]
	public class DeflectingCombatBehaviourAsset : CorePerformanceMoveBehaviourAsset
	{
		protected bool InWindow =>
			Performer.PrepTime > Move.MinPrep * windowShift &&
			Performer.PrepTime < Move.MinPrep * windowShift + deflectWindow;

		[SerializeField, Range(0f, 1f)] private float deflectWindow = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("0 is at beginning of charge, 1 is at end of minimum charge.")] private float windowShift = 1f;

		private AgentStatHandler agentStatHandler;
		private IHittable hittable;

		private FloatFuncModifier enduranceDamageMod;
		private bool deflected;
		private float deflectTime;

		public void InjectDependencies(AgentStatHandler agentStatHandler, IHittable hittable)
		{
			this.agentStatHandler = agentStatHandler;
			this.hittable = hittable;
		}

		public override void Start()
		{
			base.Start();

			enduranceDamageMod = new FloatFuncModifier(ModMethod.Absolute, (damage) => damage * (InWindow ? 0f : 1f));
			agentStatHandler.PointStats.W.Cost.AddModifier(this, enduranceDamageMod);

			deflected = false;
			deflectTime = 0f;

			hittable.Subscribe(this, OnHitEvent, 1000);
		}

		public override void Stop()
		{
			base.Stop();

			agentStatHandler.PointStats.W.Cost.RemoveModifier(this);
			enduranceDamageMod.Dispose();
			hittable.Unsubscribe(this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (Performer.State == PerformanceState.Preparing)
			{
				// Drain charge stat.
				if (Agent.Stats.TryApplyStatCost(Move.PrepCost.Stat, Move.PrepCost.Cost * delta, false, out _, out bool drained, out _) && drained)
				{
					Performer.TryPerform();
				}
			}
		}

		protected override IPoserInstructions Evaluate(out float weight)
		{
			if (Move.PosingData is PoseSequence sequence)
			{
				// Charging.
				IPose chargePose = sequence.Get(0);
				float chargeWeight = chargePose.EvaluateTransition(Mathf.Clamp01(Performer.PrepTime / Move.MaxPrep));

				weight = chargeWeight * Performer.Weight;

				return new PoserInstructions(sequence.Evaluate(deflected ? Performer.PrepTime - deflectTime : 0f));
			}
			else
			{
				SpaxDebug.Error("Behaviour only supports PoseSequence as PosingData", $"Selected: {Move.PosingData.GetType().FullName}");
				weight = 0f;
				return null;
			}
		}

		private void OnHitEvent(HitData hitData)
		{
			if (InWindow)
			{
				hitData.Data.SetValue(HitDataIdentifiers.DEFLECTED, true);
				deflected = true;
				deflectTime = Performer.PrepTime;
			}
		}
	}
}
