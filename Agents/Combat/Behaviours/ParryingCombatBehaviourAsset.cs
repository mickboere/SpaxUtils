using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that adjust the Agent's stats while guarding.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_Parrying", menuName = "ScriptableObjects/Combat/ParryingCombatBehaviourAsset")]
	public class ParryingCombatBehaviourAsset : CorePerformanceMoveBehaviourAsset
	{
		// Own clock in ChargeTime units: the real one freezes when the state leaves Preparing, which let
		// a tap hold the window open for the rest of the move.
		protected bool InWindow =>
			elapsed > Move.MinCharge * windowShift &&
			elapsed < Move.MinCharge * windowShift + window;

		/// <summary>The window has been and gone with nothing parried.</summary>
		protected bool WindowClosed => elapsed >= Move.MinCharge * windowShift + window;

		[Header("Parrying")]
		[SerializeField, Range(0f, 1f), Tooltip("0 is at beginning of charge, 1 is at end of minimum charge.")]
		private float windowShift = 0.5f;
		[SerializeField, Range(0f, 1f), Tooltip("Leading fraction of the window that counts as perfect: no endurance or health lost.")]
		private float perfectFraction = 0.2f;
		[SerializeField, Tooltip("Clip seconds the reaction jumps to the instant a parry lands, so the arm reacts before the hit-paused clock catches up.")]
		private float reactionLead = 0.015f;
		[SerializeField, Tooltip("Logs the whole parry lifecycle per frame to Debuddy.")] private bool debug;

		private AgentStatHandler agentStatHandler;
		private ResourceStat chargeStat;
		private EntityStat window;
		private EntityStat chargeSpeed;
		private IHittable hittable;
		private IAgentMovementHandler movementHandler;

		private bool parried;
		private float parryTime;
		private float elapsed;

		public void InjectDependencies(AgentStatHandler agentStatHandler,
		IHittable hittable, IAgentMovementHandler movementHandler)
		{
			this.agentStatHandler = agentStatHandler;
			this.hittable = hittable;
			this.movementHandler = movementHandler;

			agentStatHandler.TryGetResourceStat(Move.ChargeCost.Stat, out chargeStat);
			Agent.Stats.TryGetStat(AgentStatIdentifiers.WINDOW, out window);
			chargeSpeed = Agent.Stats.GetStat(Move.ChargeSpeedMultiplierStat, false);
		}

		public override void Start()
		{
			base.Start();

			parried = false;
			parryTime = 0f;
			elapsed = 0f;

			// Held from the very first frame: the clock runs BEFORE behaviours, so waiting until the
			// performance has started already lets one frame of the reaction through.
			Performer.Paused = true;

			hittable.Subscribe(this, OnHitEvent, -1000);
		}

		public override void Stop()
		{
			base.Stop();

			hittable.Unsubscribe(this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			// Ticks in ChargeTime units, so a faster charger reaches the window sooner.
			elapsed += delta * (chargeSpeed != null ? chargeSpeed.Value : 1f);

			if (debug)
			{
				Trace();
			}

			if (Performer.State == PerformanceState.Preparing)
			{
				// Drain charge stat.
				if (chargeStat != null)
				{
					chargeStat.Drain(Move.ChargeCost.Cost * delta, out bool drained);
					if (drained) Performer.TryPerform();
				}
				return;
			}

			// Released without parrying anything: the parry is over.
			if (!parried)
			{
				Performer.TryCancel(true);
			}
		}

		protected override IPoserInstructions Evaluate(out float weight)
		{
			if (Move.PosingData is PoseSequence sequence)
			{
				// Charging.
				IPose chargePose = sequence.Get(0);
				float chargeWeight = chargePose.EvaluateTransition(Mathf.Clamp01(Performer.ChargeTime / Move.ChargeDuration));

				weight = chargeWeight * Performer.Weight;

				return new PoserInstructions(sequence.Evaluate(parried ? Performer.ChargeTime - parryTime : 0f));
			}
			else
			{
				SpaxDebug.Error("Behaviour only supports PoseSequence as PosingData", $"Selected: {Move.PosingData.GetType().FullName}");
				weight = 0f;
				return null;
			}
		}

		/// <summary>Every quantity the parry depends on, so a failure can be located instead of guessed at.</summary>
		private void Trace()
		{
			float open = Move.MinCharge * windowShift;
			SpaxDebug.Log("PARRY",
				$"t:{elapsed:0.000} window:{open:0.000}..{open + window:0.000} in:{InWindow} closed:{WindowClosed} " +
				$"parried:{parried} state:{Performer.State} charge:{Performer.ChargeTime:0.000}/{Move.MinCharge:0.000} " +
				$"run:{Performer.RunTime:0.000}/{Move.MinDuration:0.000}+{Move.Release:0.000} " +
				$"paused:{Performer.Paused} weight:{Weight:0.00} head:{(TimelinePlayer == null ? -1f : TimelinePlayer.Time):0.000}");
		}

		/// <summary>Timing quality 0-1: rises to window-open, holds over the perfect span, falls to 0 at close.</summary>
		private float Quality()
		{
			float open = Move.MinCharge * windowShift;
			float perfect = open + window * perfectFraction;
			float close = open + window;

			if (elapsed < open)
			{
				return Mathf.Clamp01(elapsed / open);
			}
			if (elapsed <= perfect)
			{
				return 1f;
			}
			return close > perfect ? Mathf.Clamp01(1f - (elapsed - perfect) / (close - perfect)) : 0f;
		}

		private void OnHitEvent(HitData hitData)
		{
			// Held means still charging; any hit while held is parried, timing only sets the cost.
			if (Performer.State == PerformanceState.Preparing)
			{
				hitData.Data.SetValue(HitDataIdentifiers.PARRIED, true);
				hitData.Data.SetValue(HitDataIdentifiers.PARRY_QUALITY, Quality());
				parried = true;
				parryTime = Performer.ChargeTime;

				// Jump straight into the reaction: the clock is hit-paused to a crawl, so waiting for RunTime
				// to reach the swing would delay the parry by the entire pause.
				Performer.PerformNow(reactionLead);

				// Released here, not next frame: the reaction must not miss the frame it was earned on.
				Performer.Paused = false;

				movementHandler.ForceRotation(hitData.Hitter.Transform.position - Agent.Transform.position);

				if (debug)
				{
					SpaxDebug.Log("PARRY hit", $"elapsed:{elapsed:0.000} quality:{Quality():0.00} chargeTime:{Performer.ChargeTime:0.000}");
				}
			}
		}
	}
}
