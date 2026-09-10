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
		// Own clock in ChargeTime units: the real one freezes when the state leaves Preparing, which let
		// a tap hold the window open for the rest of the move.
		protected bool InWindow =>
			elapsed > Move.MinCharge * windowShift &&
			elapsed < Move.MinCharge * windowShift + window;

		/// <summary>The window has been and gone with nothing deflected.</summary>
		protected bool WindowClosed => elapsed >= Move.MinCharge * windowShift + window;

		[Header("Deflecting")]
		[SerializeField, Range(0f, 1f), Tooltip("0 is at beginning of charge, 1 is at end of minimum charge.")]
		private float windowShift = 0.5f;
		[SerializeField, Tooltip("Clip seconds the reaction jumps to the instant a deflect lands, so the arm reacts before the hit-paused clock catches up.")]
		private float reactionLead = 0.015f;
		[SerializeField, Tooltip("Logs the whole deflect lifecycle per frame to Debuddy.")] private bool debug;

		private AgentStatHandler agentStatHandler;
		private PointsStat chargeStat;
		private EntityStat window;
		private EntityStat chargeSpeed;
		private IHittable hittable;
		private IAgentMovementHandler movementHandler;

		private bool deflected;
		private float deflectTime;
		private float elapsed;

		public void InjectDependencies(AgentStatHandler agentStatHandler,
		IHittable hittable, IAgentMovementHandler movementHandler)
		{
			this.agentStatHandler = agentStatHandler;
			this.hittable = hittable;
			this.movementHandler = movementHandler;

			agentStatHandler.TryGetPointStat(Move.ChargeCost.Stat, out chargeStat);
			Agent.Stats.TryGetStat(AgentStatIdentifiers.WINDOW, out window);
			chargeSpeed = Agent.Stats.GetStat(Move.ChargeSpeedMultiplierStat, false);
		}

		public override void Start()
		{
			base.Start();

			deflected = false;
			deflectTime = 0f;
			elapsed = 0f;

			// Held from the very first frame: the clock runs BEFORE behaviours, so waiting until the
			// performance has started already lets one frame of the reaction through.
			Performer.Paused = true;

			hittable.Subscribe(this, OnHitEvent, 1000);
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

			// Only a window that CLOSED empty is a failure; leaving Preparing never was.
			if (!deflected && WindowClosed)
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

				return new PoserInstructions(sequence.Evaluate(deflected ? Performer.ChargeTime - deflectTime : 0f));
			}
			else
			{
				SpaxDebug.Error("Behaviour only supports PoseSequence as PosingData", $"Selected: {Move.PosingData.GetType().FullName}");
				weight = 0f;
				return null;
			}
		}

		/// <summary>Every quantity the deflect depends on, so a failure can be located instead of guessed at.</summary>
		private void Trace()
		{
			float open = Move.MinCharge * windowShift;
			SpaxDebug.Log("DEFLECT",
				$"t:{elapsed:0.000} window:{open:0.000}..{open + window:0.000} in:{InWindow} closed:{WindowClosed} " +
				$"deflected:{deflected} state:{Performer.State} charge:{Performer.ChargeTime:0.000}/{Move.MinCharge:0.000} " +
				$"run:{Performer.RunTime:0.000}/{Move.MinDuration:0.000}+{Move.Release:0.000} " +
				$"paused:{Performer.Paused} weight:{Weight:0.00} head:{(TimelinePlayer == null ? -1f : TimelinePlayer.Time):0.000}");
		}

		private void OnHitEvent(HitData hitData)
		{
			if (InWindow)
			{
				hitData.Data.SetValue(HitDataIdentifiers.DEFLECTED, true);
				deflected = true;
				deflectTime = Performer.ChargeTime;

				// Jump straight into the reaction: the clock is hit-paused to a crawl, so waiting for RunTime
				// to reach the swing would delay the parry by the entire pause.
				Performer.PerformNow(reactionLead);

				// Released here, not next frame: the reaction must not miss the frame it was earned on.
				Performer.Paused = false;

				movementHandler.ForceRotation(hitData.Hitter.Transform.position - Agent.Transform.position);

				if (debug)
				{
					SpaxDebug.Log("DEFLECT hit", $"elapsed:{elapsed:0.000} chargeTime:{Performer.ChargeTime:0.000}");
				}
			}
		}
	}
}
