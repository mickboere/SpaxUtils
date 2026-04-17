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
			Performer.ChargeTime > Move.MinCharge * windowShift &&
			Performer.ChargeTime < Move.MinCharge * windowShift + deflectWindow;

		[SerializeField, Range(0f, 1f)] private float deflectWindow = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("0 is at beginning of charge, 1 is at end of minimum charge.")] private float windowShift = 1f;

		private AgentStatHandler agentStatHandler;
		private PointsStat chargeStat;
		private IHittable hittable;

		private FloatFuncModifier enduranceDamageMod;
		private bool deflected;
		private float deflectTime;

		public void InjectDependencies(AgentStatHandler agentStatHandler, IHittable hittable)
		{
			this.agentStatHandler = agentStatHandler;
			this.hittable = hittable;

			agentStatHandler.TryGetPointStat(Move.ChargeCost.Stat, out chargeStat);
		}

		public override void Start()
		{
			base.Start();

			enduranceDamageMod = new FloatFuncModifier(ModMethod.Absolute, (damage) => damage * (InWindow ? 0f : 1f));
			agentStatHandler.PointStats.W.DrainMult.AddModifier(this, enduranceDamageMod);

			deflected = false;
			deflectTime = 0f;

			hittable.Subscribe(this, OnHitEvent, 1000);
		}

		public override void Stop()
		{
			base.Stop();

			agentStatHandler.PointStats.W.DrainMult.RemoveModifier(this);
			enduranceDamageMod.Dispose();
			hittable.Unsubscribe(this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (Performer.State == PerformanceState.Preparing)
			{
				// Drain charge stat.
				if (chargeStat != null)
				{
					chargeStat.Drain(Move.ChargeCost.Cost * delta, out bool drained);
					if (drained) Performer.TryPerform();
				}
			}
		}

		protected override IPoserInstructions Evaluate(out float weight)
		{
			if (Move.PosingData is PoseSequence sequence)
			{
				// Charging.
				IPose chargePose = sequence.Get(0);
				float chargeWeight = chargePose.EvaluateTransition(Mathf.Clamp01(Performer.ChargeTime / Move.MaxCharge));

				weight = chargeWeight * Performer.Weight;

				return new PoserInstructions(sequence.Evaluate(deflected ? Performer.ChargeTime - deflectTime : 0f));
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
				deflectTime = Performer.ChargeTime;
			}
		}
	}
}
