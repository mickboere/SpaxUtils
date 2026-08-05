using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for 3-part sequenced melee combat moves (charge, perform, release).
	/// </summary>
	public interface IMeleeCombatMove : ICombatMove
	{
		/// <summary>
		/// Collection of hit-box bone identifiers for this move's performance.
		/// </summary>
		List<string> HitBoxes { get; }

		/// <summary>
		/// Time into combat performance before hit detection is activated.
		/// </summary>
		float HitDetectionDelay { get; }

		#region Momentum

		/// <summary>
		/// Which way this strike travels, in the agent's local space. The single source of a move's geometry —
		/// it decides the knockback direction, how far the move lunges, how hard it shoves, and how it must be
		/// dodged. Replaces the old AttackDirection enum, HitDirection and Inertia, which each described a
		/// different facet of the same thing and could disagree.
		///		- z THRUST: −1 withdraw ... +1 lunge. Only the forward half buys stick range.
		///		- y LIFT: −1 chop down ... +1 rise.
		///		- x SWEEP: −1 left ... +1 right. Produces a small lateral step, never enough to lose the target.
		/// MAGNITUDE IS COMMITMENT: a (0,0,1) thrust and a (0,0,0.4) jab differ in lunge and shove with no
		/// second field. Zero-length means no strike geometry at all — the hit falls back to the swept
		/// contact direction and the move neither lunges nor shoves.
		/// </summary>
		Vector3 StrikeDirection { get; }

		/// <summary>
		/// The delay before the move's approach begins, into the performance runtime.
		/// </summary>
		public float InertiaDelay { get; }

		/// <summary>
		/// Whether the charge pose should be held until done applying momentum.
		/// </summary>
		public bool PrelongCharge { get; }

		/// <summary>
		/// The velocity above which the performance should be prolonged.
		/// </summary>
		public float ProlongThreshold { get; }

		#endregion Momentum

		#region Stats

		/// <summary>
		/// Sub-Stat identifying the limb responsible for performing hits.
		/// </summary>
		string Limb { get; }

		/// <summary>
		/// Fraction of total body mass this strike commits; blends the limb mass toward whole-body for the hit.
		/// Doubles as the hit mass for limb-less strikes (kicks, body rams) with no MASS substat.
		/// </summary>
		float BodyMassFraction { get; }

		/// <summary>
		/// When true, the weapon equipped on <see cref="Limb"/> contributes its PhysicsDistribution to the hit.
		/// When false (pommel strikes, kicks, etc.), body physics only.
		/// </summary>
		bool UseArmament { get; }

		/// <summary>
		/// Percentage of user's Slash that gets transfered into the attack. UNARMED ONLY — when
		/// <see cref="UseArmament"/> is true the weapon alone decides which axes the strike fulfills.
		/// </summary>
		float Slash { get; }

		/// <summary>
		/// Percentage of user's Power that gets transfered into the attack. UNARMED ONLY — see <see cref="Slash"/>.
		/// </summary>
		float Power { get; }

		/// <summary>
		/// Percentage of user's Pierce that gets transfered into the attack. UNARMED ONLY — see <see cref="Slash"/>.
		/// </summary>
		float Pierce { get; }

		/// <summary>
		/// Scalar on the move's TOTAL output; 1 is a normal strike, above 1 a special/finisher that hits harder
		/// than the base stats allow. Never touches distribution — what the strike hits WITH stays the weapon's
		/// call (or the sliders' when unarmed).
		/// </summary>
		float OutputScale { get; }

		/// <summary>
		/// When true, this move uses its own <see cref="ChargeBalance"/>/<see cref="PerformBalance"/>
		/// instead of the global defaults in <see cref="CombatSettings"/>.
		/// </summary>
		bool OverrideBalance { get; }

		/// <summary>
		/// How much balance is maintained while charging. Only used when <see cref="OverrideBalance"/> is true.
		/// </summary>
		float ChargeBalance { get; }

		/// <summary>
		/// How much balance is maintained while performing. Only used when <see cref="OverrideBalance"/> is true.
		/// </summary>
		float PerformBalance { get; }

		#endregion Stats
	}
}
