using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable <see cref="IMeleeCombatMove"/> asset that contains data required for performing a melee combat move.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(MeleeCombatMove), menuName = "Performance/Combat/" + nameof(MeleeCombatMove))]
	public class MeleeCombatMove : BaseCombatMove, IMeleeCombatMove
	{
		public List<string> HitBoxes => hitBoxes;
		public float HitDetectionDelay => hitDetectionDelay;
		/// <inheritdoc/>
		/// <remarks>
		/// Assembled from the three axis sliders. Clamped to unit length so a move using several axes at once
		/// cannot silently read as over-committed (three maxed sliders would otherwise magnitude to 1.73).
		/// </remarks>
		public Vector3 StrikeDirection => Vector3.ClampMagnitude(new Vector3(sweep, lift, thrust), 1f);
		public float InertiaDelay => inertiaDelay;
		public bool PrelongCharge => prelongCharge;
		public float ProlongThreshold => prolongThreshold;
		public string Limb => limb;
		public float BodyMassFraction => bodyMassFraction;
		public bool UseArmament => useArmament;
		public float Slash => slash;
		public float Power => power;
		public float Pierce => pierce;
		public float OutputScale => outputScale;
		public bool OverrideBalance => overrideBalance;
		public float ChargeBalance => chargeBalance;
		public float PerformBalance => performBalance;

		[Header("Hit detection")]
		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), showAdress: true)] private List<string> hitBoxes;
		[SerializeField] private float hitDetectionDelay = 0f;

		[Header("Momentum")]
		// Which way this strike travels, in the agent's local space — the single source of the move's geometry.
		// It decides the knockback direction, how far the move lunges, how hard it shoves and how it must be
		// dodged. HOW FAR EACH SLIDER IS PUSHED IS COMMITMENT: a thrust of 1 is a full lunge, 0.4 a light jab.
		// All three at zero means no geometry at all — the hit falls back to the swept contact direction and the
		// move neither lunges nor shoves.
		[SerializeField, Range(-1f, 1f), Tooltip("SWEEP — how far the strike travels sideways. -1 swings fully left, +1 fully right. Produces a small lateral step, deliberately never large enough to slide the attack off its target.")]
		private float sweep;
		[SerializeField, Range(-1f, 1f), Tooltip("LIFT — how far the strike travels vertically. -1 chops straight down, +1 rises (uppercut).")]
		private float lift;
		[SerializeField, Range(-1f, 1f), Tooltip("THRUST — how far the strike travels forward. +1 is a full lunge, -1 withdraws. Only the forward half buys stick range; a withdrawing strike never lunges into the target.")]
		private float thrust = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("BODY MASS — how much of the body this strike commits, blending the limb mass toward whole-body for the hit. Drives knockback force. Also the hit mass for limb-less strikes (kicks, body rams).")]
		private float bodyMassFraction = 0.25f;
		[SerializeField, FormerlySerializedAs("forceDelay")] private float inertiaDelay;
		[SerializeField, Tooltip("Whether the charge pose should be held until within attack release range.")] private bool prelongCharge;
		[SerializeField, Tooltip("Velocity above which the performance should be prolonged.")] private float prolongThreshold = 1f;

		[Header("Stats")]
		[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants), true)] private string limb;
		[SerializeField] private bool useArmament = false;
		// Unarmed distribution only. An armed strike takes its axes wholly from the weapon (its equipment
		// distribution, as realized by this move's swing/thrust usage), so these would have nothing to say.
		[SerializeField, Conditional(nameof(useArmament), inverse: true), Range(0f, 1f), Tooltip("Percentage of user's Slash transfered into hit. Unarmed only — an armed strike takes its distribution from the weapon."), FormerlySerializedAs("offence"), FormerlySerializedAs("piercing")] private float slash = 1f;
		[SerializeField, Conditional(nameof(useArmament), inverse: true), Range(0f, 1f), Tooltip("Percentage of user's Power transfered into hit. Unarmed only.")] private float power = 1f;
		[SerializeField, Conditional(nameof(useArmament), inverse: true), Range(0f, 1f), Tooltip("Percentage of user's Pierce transfered into hit. Unarmed only."), FormerlySerializedAs("precision")] private float pierce = 1f;
		[SerializeField, Min(0f), Tooltip("Scalar on TOTAL output. 1 is a normal strike; above 1 is a special/finisher hitting harder than the base stats allow. Does not affect which damage types the strike deals.")] private float outputScale = 1f;
		[SerializeField, Tooltip("When enabled, this move uses its own balance values below instead of the global defaults in CombatSettings.")] private bool overrideBalance = false;
		[SerializeField, Conditional(nameof(overrideBalance)), Range(0.01f, 1f), Tooltip("How much balance is maintained while charging.")] private float chargeBalance = 1f;
		[SerializeField, Conditional(nameof(overrideBalance)), Range(0.01f, 1f), Tooltip("How much balance is maintained while performing.")] private float performBalance = 1f;
	}
}
