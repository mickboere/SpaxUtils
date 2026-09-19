using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Collection of standardized formulas used in Spirit Axis.
	/// </summary>
	public static class SpaxFormulas
	{
		// EXP curve: points = (level / CONSTANT) ^ POWER. Anchored so level 10 costs ~10k EXP.
		// POWER above 2 makes the per-level cost grow rather than flatten, leaving room for a talent multiplier.
		public const float CONSTANT = 0.25f;
		public const float POWER = 2.5f;
		public const float SCALE = 100f;

		// A bar's EXP worth doubles every EXP_BAR_KNEE levels, making effort per level grow as ~sqrt(level).
		// Shape only; the magnitude is ExpSettings.ExpPerBar.
		public const float EXP_BAR_KNEE = 10f;

		// A physical deed feeds the body whole and the soul half; spellwork inverts this per call.
		public const float SOUL_SHARE_PHYSICAL = 0.5f;

		// Scale/shift constants for converting EXP levels to physics and resource values.
		// SCALE ratio sets the hits-to-kill ceiling; the SHIFTs are flat floors that stretch the climb to it.
		public const float RESOURCE_SCALE = 8f;
		public const float RESOURCE_SHIFT = 32f;
		public const float PHYSIC_SCALE = 1f;
		public const float PHYSIC_SHIFT = 8f;

		// Gear mass per rank: a 1kg rank-1 weapon weighs 25kg by rank 100, a 5kg armour set 50kg.
		public const float WEAPON_MASS_PER_RANK = 24f / 99f;
		public const float APPAREL_MASS_PER_RANK = 45f / 99f;

		// The body force measures against, and the share of it hanging on one hand.
		public const float BASE_BODY_MASS = 100f;
		public const float LIMB_MASS_FRACTION = 0.01f;

		// What an average rank carries: half a physical distribution, half an armour set, a 1kg weapon.
		// Exertion judges against a FULL-power weapon instead, so swings per bar hold at every rank.
		public const float REFERENCE_GEAR_SHARE = 0.5f;
		public const float EXERTION_GEAR_SHARE = 1f;
		public const float REFERENCE_ARMOR_MASS = 5f;
		public const float REFERENCE_WEAPON_MASS = 1f;

		// How much an equipment lane leans on QUALITY. Power is weight and shape; a point needs less honing than an edge.
		public const float QUALITY_BIAS_POWER = 0.25f;
		public const float QUALITY_BIAS_PIERCE = 0.75f;

		// Softens the quality curve: a worn item keeps more of its physics, a mythic one gains less.
		public const float QUALITY_EXPONENT = 0.66f;

		#region Combat

		/// <summary>
		/// Standard damage formula taking an offence value and a defence value to calculate damage.
		/// </summary>
		/// <param name="offence">The attacker's offensive power.</param>
		/// <param name="defence">The defender's defensive power.</param>
		//public static float CalculateDamage(float offence, float defence)
		//{
		//	// 1. Linear Term: "The Armor Check"
		//	float linear = (offence - defence);
		//	if (linear < 0f)
		//	{
		//		linear *= 0.1f; // Soften negative pull.
		//	}

		//	// 2. Curved Term: "The Mitigation Check"
		//	float mitigationFactor = SCALE / (SCALE + defence);
		//	float expo = offence * mitigationFactor;

		//	// 3. Blend based on exponence.
		//	return Mathf.Lerp(linear, expo, 0.5f).Max(0f);
		//}

		/// <summary>
		/// Damage formula using an offence-relative Hill curve:
		/// damage = offence / (1 + crossFactor * (defence/offence)^exponent).
		///
		/// crossFactor sets the damage fraction when offence == defence:
		/// damage(off==def) = offence / (1 + crossFactor).
		/// Example: crossFactor=3 -> 25% at equality.
		///
		/// exponent controls steepness of falloff as defence exceeds offence (higher = steeper).
		/// </summary>
		/// <param name="offence">Attacker offensive power (>= 0).</param>
		/// <param name="defence">Defender defensive power (>= 0).</param>
		/// <param name="crossFactor">
		/// Controls the equality point.
		/// CrossFactor=1 means offence==defence yields 50% of offence.
		/// CrossFactor=3 means offence==defence yields 25% of offence.
		/// </param>
		/// <param name="exponent">Steepness (>= 0). Try 2..6.</param>
		/// <returns>Damage (>= 0).</returns>
		public static float CalculateDamage(float offence,
			float defence,
			float crossFactor = 1f,
			float exponent = 1f)
		{
			float o = Mathf.Max(0f, offence);
			float d = Mathf.Max(0f, defence);

			if (o <= 0.0001f)
			{
				return 0f;
			}

			float c = Mathf.Max(0f, crossFactor);
			float n = Mathf.Max(0.0001f, exponent);

			float ratio = d / o;
			float denom = 1f + c * Mathf.Pow(ratio, n);

			return o / denom;
		}

		/// <summary>
		/// The share (0..1) of <paramref name="offence"/> that gets past <paramref name="defence"/>: o^k / (o^k + d^k).
		/// 0.5 at an equal match at any <paramref name="exponent"/>; higher steepens the contest around it.
		/// </summary>
		public static float Transfer(float offence, float defence, float exponent = 1f)
		{
			float o = Mathf.Max(0f, offence);
			if (o <= 0.0001f)
			{
				return 0f;
			}
			float d = Mathf.Max(0f, defence);
			return exponent == 1f ? o / (o + d) : 1f / (1f + Mathf.Pow(d / o, exponent));
		}

		/// <summary>
		/// Two contests of one damage channel, softened by <paramref name="power"/>; below 1 a lopsided match compounds less.
		/// An even match (both at 0.5) deals the same at any power.
		/// </summary>
		public static float Contests(float a, float b, float power)
		{
			return Mathf.Pow(Mathf.Max(0f, a * b), power) * Mathf.Pow(4f, power - 1f);
		}

		/// <summary>
		/// How cleanly a point couples into the target (0..1). Yield walls it <paramref name="yieldPivot"/> extra
		/// times over, proportionally, so the contest holds at every level; crit is the payback for that wall.
		/// </summary>
		public static float CalculateCoupling(float pierce, float yield, float yieldPivot)
		{
			float pi = Mathf.Max(0f, pierce);
			float yi = Mathf.Max(0f, yield) * (1f + Mathf.Max(0f, yieldPivot));

			return pi <= 0.0001f ? 0f : Mathf.Clamp01(pi / (pi + yi));
		}

		/// <summary>
		/// Calculates the final critical-hit chance using a Pierce vs Yield coupling contest,
		/// a smoothed Luck contest, and an anatomy-driven Vulnerability remap.
		/// Vulnerability anchors the output: 0 -> 0% crit, 0.5 -> base chance, 1 -> 100% crit.
		/// The returned value is clamped to 0..1.
		/// </summary>
		/// <param name="coupling">Coupling factor from <see cref="CalculateCoupling"/> (0..1).</param>
		/// <param name="vulnerability01">
		/// Anatomy vulnerability in 0..1. Remaps chance so 0 disables crits, 0.5 leaves base chance unchanged,
		/// and 1 guarantees a crit.
		/// </param>
		/// <param name="attackerLuck">Attacker Luck (>= 0). Competes with defenderLuck in the luck contest.</param>
		/// <param name="defenderLuck">Defender Luck (>= 0). Competes with attackerLuck in the luck contest.</param>
		/// <param name="luckPivot">
		/// Smoothing constant for the luck contest (default 0.1). Lower makes luck differences matter more.
		/// </param>
		/// <returns>Final crit chance in 0..1.</returns>
		public static float CalculateCritChance(
			float coupling,
			float vulnerability01 = 0.5f,
			float attackerLuck = 0f,
			float defenderLuck = 0f,
			float luckPivot = 0.1f)
		{
			// 1) Luck contest (smoothed).
			float attLuck = Mathf.Max(0f, attackerLuck);
			float defLuck = Mathf.Max(0f, defenderLuck);
			float luckFactor = (attLuck + luckPivot) / (attLuck + defLuck + 2f * luckPivot);
			luckFactor = Mathf.Clamp01(luckFactor);

			// 2) Default crit chance at vulnerability = 0.5.
			float baseChance = Mathf.Clamp01(coupling * luckFactor);

			// 3) Vulnerability remap:
			// v=0 -> 0, v=0.5 -> baseChance, v=1 -> 1.
			float vulnerability = Mathf.Clamp01(vulnerability01);
			float critChance;
			if (vulnerability <= 0.5f)
			{
				critChance = vulnerability * 2f * baseChance;
			}
			else
			{
				critChance = baseChance + (vulnerability - 0.5f) * 2f * (1f - baseChance);
			}

			return Mathf.Clamp01(critChance);
		}

		#endregion Combat

		#region Standardized Formulas

		/// <summary>
		/// Calculates the required EXP points for the given level.
		/// </summary>
		public static float PointsFromLevel(float level)
		{
			return Exp(level, CONSTANT, POWER);
		}

		/// <summary>
		/// Calculates the level from the given EXP points.
		/// </summary>
		public static float LevelFromPoints(float points)
		{
			return InvExp(points, CONSTANT, POWER);
		}

		/// <summary>
		/// Calculates the (EXP) points budget for the given rank.
		/// </summary>
		public static float PointsFromRank(float rank)
		{
			return 8f * PointsFromLevel(rank);
		}

		/// <summary>
		/// Inverse of <see cref="PointsFromRank"/>: the balanced-equivalent rank a total points pool represents.
		/// </summary>
		public static float RankFromPoints(float points)
		{
			return LevelFromPoints(points / 8f);
		}

		/// <summary>
		/// Cost of manually buying one attribute level, priced at the character's rank rather than the
		/// attribute's own level — so pulling a neglected attribute up costs the same as advancing a
		/// specialized one. Flat across all 8 attributes; a purchase always grants exactly one level from
		/// wherever the attribute currently sits, so naturally banked progress carries over instead of being
		/// paid for or discarded.
		/// </summary>
		/// <param name="totalPoints">Summed EXP across all 8 attributes.</param>
		public static float LevelUpCost(float totalPoints)
		{
			// Double precision: the float32 Pow round-trip drifts enough (~2e-4) that an exact integer price
			// lands just above it, which Ceil then turns into a whole extra point of currency.
			// Exp(rank) IS rankPoints by definition, so subtract it directly rather than recomputing it.
			double rankPoints = Mathf.Max(0f, totalPoints) / 8.0;
			double rank = System.Math.Pow(rankPoints, 1.0 / POWER) * CONSTANT;

			return (float)(System.Math.Pow((rank + 1.0) / CONSTANT, POWER) - rankPoints);
		}

		/// <summary>
		/// <see cref="LevelUpCost"/> rounded up to a whole point, with a small epsilon so residual float noise
		/// on an exact-integer price can't tip it to the next point. The value actually charged/displayed.
		/// </summary>
		public static int LevelUpCostCeiled(float totalPoints)
		{
			return Mathf.CeilToInt(LevelUpCost(totalPoints) - 0.001f);
		}

		/// <summary>
		/// Allocates an EXP-points budget over an octad so that AFTER LevelFromPoints(),
		/// the resulting levels preserve the same ratios as the input distribution.
		/// Rule: only normalize if sum > 1 (otherwise sum acts as "coverage").
		/// </summary>
		public static Vector8 AllocatePointsForLevelRatios(Vector8 distribution, float totalPoints)
		{
			Vector8 ratioW;
			float coverage;
			GetRatioWeightsAndCoverage(distribution, out ratioW, out coverage);

			float spend = totalPoints * coverage;
			if (spend <= 0f)
			{
				return Vector8.Zero;
			}

			// Pre-distort in EXP-space so that InvExp() yields levels proportional to ratioW.
			Vector8 wp = PowNonNegative(ratioW, POWER);

			float sumWp = wp.Sum();
			if (sumWp <= 0f)
			{
				return Vector8.Zero;
			}

			Vector8 expWeights = wp / sumWp;
			return expWeights * spend;
		}

		/// <summary>
		/// Same as AllocatePointsForLevelRatios(distribution, totalPoints) but uses rank as input.
		/// </summary>
		public static Vector8 AllocatePointsFromRankForLevelRatios(Vector8 distribution, float rank)
		{
			return AllocatePointsForLevelRatios(distribution, PointsFromRank(rank));
		}

		private static void GetRatioWeightsAndCoverage(Vector8 distribution, out Vector8 ratioWeights, out float coverage)
		{
			float sum = 0f;
			for (int i = 0; i < 8; i++)
			{
				float v = Mathf.Max(0f, distribution[i]);
				sum += v;
			}

			if (sum <= 0f)
			{
				ratioWeights = Vector8.Zero;
				coverage = 0f;
				return;
			}

			coverage = sum > 1f ? 1f : sum;

			// Always normalize for ratios (coverage handles the "sum <= 1" case).
			ratioWeights = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float v = Mathf.Max(0f, distribution[i]);
				ratioWeights[i] = v / sum;
			}
		}

		private static Vector8 PowNonNegative(Vector8 v, float p)
		{
			Vector8 r = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float x = Mathf.Max(0f, v[i]);
				r[i] = x <= 0f ? 0f : Mathf.Pow(x, p);
			}
			return r;
		}

		public static float LevelToResource(float level, bool shift = true)
			=> level * RESOURCE_SCALE + (shift ? RESOURCE_SHIFT : 0f);

		public static float LevelToPhysic(float level, bool shift = true)
			=> level * PHYSIC_SCALE + (shift ? PHYSIC_SHIFT : 0f);

		/// <summary>
		/// A full resource bar's EXP worth at <paramref name="level"/>, as a multiple of its level-0 worth.
		/// </summary>
		public static float ExpBarScale(float level)
			=> 1f + Mathf.Max(0f, level) / EXP_BAR_KNEE;

		/// <summary>
		/// Equipment mass. The authored <paramref name="baseMass"/> is its weight at rank 1, growing linearly by
		/// how physical it is (Power/Armor share) and how much body it covers. QUALITY makes gear better, not heavier.
		/// </summary>
		public static float EquipmentMass(float baseMass, float rank, Vector8 distribution,
			float coverage, bool apparel)
		{
			float perRank = apparel ? APPAREL_MASS_PER_RANK : WEAPON_MASS_PER_RANK;
			return baseMass + perRank * Mathf.Max(0f, rank - 1f) *
				PhysicalShare(distribution) * Mathf.Max(0f, coverage);
		}

		/// <summary>
		/// How much of <paramref name="distribution"/> is physical mass: Power (N) and Armor (W) against all lanes.
		/// A pure slash/pierce item reads 0 and so never gains weight with rank.
		/// </summary>
		public static float PhysicalShare(Vector8 distribution)
		{
			float sum = 0f;
			for (int i = 0; i < 8; i++)
			{
				sum += Mathf.Max(0f, distribution[i]);
			}
			if (sum <= 0f)
			{
				return 0f;
			}
			return (Mathf.Max(0f, distribution[0]) + Mathf.Max(0f, distribution[6])) / sum;
		}

		/// <summary>Weapon mass alone: the limb substat minus the arm's own share of the body.</summary>
		public static float WeaponMass(float limbMass, float bodyMass)
			=> Mathf.Max(0f, limbMass - LIMB_MASS_FRACTION * Mathf.Max(0f, bodyMass));

		/// <summary>Body mass expected at <paramref name="rank"/>: frame, levels and half an armour set.</summary>
		public static float ExpectedBodyMass(float rank)
			=> BASE_BODY_MASS + Mathf.Max(0f, rank) + (REFERENCE_ARMOR_MASS +
				APPAREL_MASS_PER_RANK * Mathf.Max(0f, rank - 1f)) * REFERENCE_GEAR_SHARE;

		/// <summary>Limb+weapon mass expected at <paramref name="rank"/> for gear of <paramref name="share"/> Power/Armor.</summary>
		public static float ExpectedLimbMass(float rank, float share)
			=> LIMB_MASS_FRACTION * ExpectedBodyMass(rank) + REFERENCE_WEAPON_MASS +
				WEAPON_MASS_PER_RANK * Mathf.Max(0f, rank - 1f) * Mathf.Max(0f, share);

		/// <summary>Limb+weapon mass expected at <paramref name="rank"/>; what force compares a strike against.</summary>
		public static float ExpectedLimbMass(float rank)
			=> ExpectedLimbMass(rank, REFERENCE_GEAR_SHARE);

		/// <summary>
		/// Per-lane weights for equipment's SHIFT: the distribution normalized to sum 1, scaled by its
		/// EFFECTIVE lane count, so an even spread gets the full shift per lane and a lopsided one less.
		/// </summary>
		public static Vector8 EquipmentShiftWeights(Vector8 distribution)
		{
			GetRatioWeightsAndCoverage(distribution, out Vector8 ratioWeights, out _);

			// sum^2/sumSq, not a tally of non-zero lanes: a tally steps a whole unit off a token lane,
			// jumping every OTHER lane's floor with it. Equals n exactly for n equal lanes.
			float sum = 0f;
			float sumSq = 0f;
			for (int i = 0; i < 8; i++)
			{
				float v = Mathf.Max(0f, distribution[i]);
				sum += v;
				sumSq += v * v;
			}

			return sumSq > 0f ? ratioWeights * (sum * sum / sumSq) : Vector8.Zero;
		}

		/// <summary>
		/// One lane's physic: <paramref name="level"/> × <see cref="PHYSIC_SCALE"/>, plus a rank-free floor of <see cref="PHYSIC_SHIFT"/>
		/// scaled by sqrt(<paramref name="quality"/>) and <paramref name="shiftWeight"/> (see <see cref="EquipmentShiftWeights"/>).
		/// </summary>
		public static float EquipmentPhysic(float level, float quality, float shiftWeight)
			=> level * PHYSIC_SCALE + PHYSIC_SHIFT * Mathf.Sqrt(Mathf.Max(0f, quality)) * Mathf.Max(0f, shiftWeight);

		/// <summary>
		/// QUALITY as equipment lane <paramref name="lane"/> feels it: pivoted around 1 by its QUALITY_BIAS, raised to <see cref="QUALITY_EXPONENT"/>.
		/// </summary>
		public static float EquipmentLaneQuality(float quality, int lane)
		{
			float bias = lane == 0 ? QUALITY_BIAS_POWER : lane == 1 ? QUALITY_BIAS_PIERCE : 1f;
			return Mathf.Pow(Mathf.Max(0f, 1f + (quality - 1f) * bias), QUALITY_EXPONENT);
		}

		/// <summary>
		/// All 8 physics of an equipment item: rank points spread by its distribution, each lane at its own quality.
		/// </summary>
		public static Vector8 EquipmentPhysics(Vector8 distribution, float rank, float quality, float scaling)
		{
			Vector8 lanePoints = AllocatePointsForLevelRatios(distribution, PointsFromRank(rank));
			Vector8 shiftWeights = EquipmentShiftWeights(distribution);
			Vector8 physics = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float laneQuality = EquipmentLaneQuality(quality, i);
				float points = lanePoints[i] * laneQuality;
				float level = points <= 0f ? 0f : LevelFromPoints(points);
				physics[i] = Mathf.Round(EquipmentPhysic(level, laneQuality, shiftWeights[i]) * scaling);
			}
			return physics;
		}

		#endregion Standardized Formulas

		#region Standard Curves

		/// <summary>
		/// Standardized exponential formula used for converting LEVELS to EXPERIENCE.
		/// </summary>
		/// <param name="level">The level to calculate the EXP for.</param>
		/// <param name="constant">The x constant modifier.</param>
		/// <param name="power">The y power modifier.</param>
		/// <param name="round">Whether the result should be rounded to an integer.</param>
		/// <returns><paramref name="level"/> converted to EXP.</returns>
		public static float Exp(float level, float constant = CONSTANT, float power = POWER,
			bool round = false)
		{
			float Formula()
			{
				return Mathf.Pow(level / constant, power);
			}
			return round ? Mathf.Round(Formula()) : Formula();
		}

		/// <summary>
		/// Returns the EXP difference from the previous level to current <paramref name="level"/>.
		/// </summary>
		/// <param name="level">The level to calculate the EXP jump for.</param>
		/// <param name="constant">The x constant modifier.</param>
		/// <param name="power">The y power modifier.</param>
		/// <param name="round">Whether the result should be rounded to an integer.</param>
		/// <returns>The EXP difference from the previous level to current <paramref name="level"/>.</returns>
		/// <param name="implicitRound">Whether the individuals Exp results should be rounded as well.</param>
		public static float ExpDiff(float level, float constant = CONSTANT, float power = POWER,
			bool round = false, bool implicitRound = false)
		{
			float Formula()
			{
				return Exp(level, constant, power, implicitRound) - Exp(level - 1, constant, power, implicitRound);
			}
			return round ? Mathf.Round(Formula()) : Formula();
		}

		/// <summary>
		/// Standardized inversed exponential formula used for converting EXP to LEVELS.
		/// </summary>
		/// <param name="exp">The EXP to calculate the LEVEL for.</param>
		/// <param name="constant">The x constant modifier.</param>
		/// <param name="power">The y power modifier.</param>
		/// <param name="floor">Whether the result should be floored to an integer.</param>
		/// <returns><paramref name="level"/> converted to EXP.</returns>
		public static float InvExp(float exp, float constant = CONSTANT, float power = POWER,
			bool floor = false)
		{
			float Formula()
			{
				return Mathf.Pow(exp, 1f / power) * constant;
			}
			return floor ? Mathf.Floor(Formula()) : Formula();
		}

		/// <summary>
		/// Standardized logarithmic formula used for converting LEVELS to POINTS.
		/// </summary>
		/// <param name="x">The level to calculate the STAT POINTS for.</param>
		/// <param name="constant">The x constant modifier.</param>
		/// <param name="power">The y power modifier.</param>
		/// <param name="scale">The y scale modifier.</param>
		/// <param name="shift">The y shift modifier.</param>
		/// <param name="round">Whether the result should be rounded to an integer.</param>
		/// <returns><paramref name="x"/> converted to POINTS.</returns>
		public static float Log(float x, float constant = CONSTANT, float power = POWER, float scale = SCALE, float shift = 0f,
			bool round = false)
		{
			float Formula()
			{
				return shift + Mathf.Log(1f + x / constant, power) * scale;
			}
			return round ? Mathf.Round(Formula()) : Formula();
		}

		/// <summary>
		/// Returns the POINTS difference from the previous level to the current <paramref name="x"/>.
		/// </summary>
		/// <param name="x">The level to calculate the POINT jump for.</param>
		/// <param name="constant">The x constant modifier.</param>
		/// <param name="power">The y power modifier.</param>
		/// <param name="scale">The y scale modifier.</param>
		/// <param name="shift">The y shift modifier.</param>
		/// <param name="round">Whether the result should be rounded to an integer.</param>
		/// <param name="implicitRound">Whether the individuals Log results should be rounded as well.</param>
		/// <returns>The POINTS difference from the previous level to the current <paramref name="x"/>.</returns>
		public static float LogDiff(float x, float constant = CONSTANT, float power = POWER, float scale = SCALE, float shift = 0f,
			bool round = false, bool implicitRound = false)
		{
			float Formula()
			{
				return Log(x, constant, power, scale, shift, implicitRound) - Log(x - 1f, constant, power, scale, shift, implicitRound);
			}
			return round ? Mathf.Round(Formula()) : Formula();
		}

		/// <summary>
		/// Standardized inverse logarithmic formula used for converting POINTS to LEVELS.
		/// </summary>
		/// <param name="points">The calculated points to reverse.</param>
		/// <param name="constant">The x constant modifier.</param>
		/// <param name="power">The y power modifier.</param>
		/// <param name="scale">The y scale modifier.</param>
		/// <param name="shift">The y shift modifier.</param>
		/// <param name="floor">Whether the result should be floored.</param>
		/// <returns>The Level required to reach <paramref name="points"/>.</returns>
		public static float InvLog(float points, float constant = CONSTANT, float power = POWER, float scale = SCALE, float shift = 0f,
			bool floor = false)
		{
			float Formula()
			{
				// Inversion Logic:
				// y = shift + log_p(1 + x/c) * s
				// (y - shift) / s = log_p(1 + x/c)
				// p^((y - shift) / s) = 1 + x/c
				// c * (p^((y - shift) / s) - 1) = x

				float exponent = (points - shift) / scale;
				return constant * (Mathf.Pow(power, exponent) - 1f);
			}
			return floor ? Mathf.Floor(Formula()) : Formula();
		}

		/// <summary>
		/// Standardized saturating formula: rises from 0 and asymptotes to <paramref name="ceiling"/>.
		/// Output is exactly half the ceiling at <paramref name="half"/>, whatever the power.
		/// </summary>
		/// <param name="x">The input value (negatives clamp to 0).</param>
		/// <param name="ceiling">The upper value approached but never reached.</param>
		/// <param name="half">The input at which the output is half the ceiling.</param>
		/// <param name="power">Approach shape: 1 = steepest at 0, &gt;1 = S-curve, &lt;1 = sharper early knee.</param>
		public static float Saturate(float x, float ceiling = 1f, float half = 1f, float power = 1f)
		{
			float h = Mathf.Max(0.0001f, half);
			float p = Mathf.Max(0.0001f, power);
			return ceiling * (1f - Mathf.Pow(2f, -Mathf.Pow(Mathf.Max(0f, x) / h, p)));
		}

		/// <summary>
		/// Inverse of <see cref="Saturate"/>: the input required to reach <paramref name="y"/>.
		/// Returns infinity for outputs at or beyond the unreachable ceiling.
		/// </summary>
		public static float InvSaturate(float y, float ceiling = 1f, float half = 1f, float power = 1f)
		{
			if (Mathf.Abs(ceiling) < 0.0001f)
			{
				return 0f;
			}

			float h = Mathf.Max(0.0001f, half);
			float p = Mathf.Max(0.0001f, power);
			float remainder = 1f - y / ceiling;

			if (remainder <= 0f)
			{
				return Mathf.Infinity;
			}
			if (remainder >= 1f)
			{
				return 0f;
			}

			// y = c * (1 - 2^-(x/h)^p)  ->  x = h * (-log2(1 - y/c))^(1/p)
			return h * Mathf.Pow(-Mathf.Log(remainder, 2f), 1f / p);
		}

		#endregion Standard Curves

		#region Rarity

		/// <summary>
		/// Returns the quality multiplier for the given item rarity.
		/// ItemRarity: Undefined=-1, Common=0, Uncommon=1, Rare=2, Legendary=3, Mythic=4
		/// </summary>
		public static float GetRarityRange(this ItemRarity rarity)
		{
			return GetRarityRange((int)rarity);
		}

		/// <summary>
		/// Returns the quality multiplier for the given item rarity.
		/// ItemRarity: Undefined=-1, Common=0, Uncommon=1, Rare=2, Legendary=3, Mythic=4
		/// </summary>
		/// <remarks></remarks>
		public static float GetRarityRange(this int rarity)
		{
			switch (rarity)
			{
				case 0: return 0.5f;
				case 1: return 0.75f;
				case 2: return 1f;
				case 3: return 1.125f;
				case 4: return 1.25f;
				case -1:
				default:
					return 1f;
			}
		}

		public static ItemRarity GetRarityFromQuality(float quality)
		{
			if (quality <= ItemRarity.Common.GetRarityRange())
			{
				return ItemRarity.Common;
			}
			else if (quality <= ItemRarity.Uncommon.GetRarityRange())
			{
				return ItemRarity.Uncommon;
			}
			else if (quality <= ItemRarity.Rare.GetRarityRange())
			{
				return ItemRarity.Rare;
			}
			else if (quality <= ItemRarity.Legendary.GetRarityRange())
			{
				return ItemRarity.Legendary;
			}
			else
			{
				return ItemRarity.Mythic;
			}
		}

		#endregion Rarity

		#region Spiritual Alignment

		public static float GetAlignment(float sin, float virtue)
		{
			return virtue / (sin + virtue);
		}

		public static SpiritAlignment GetSpiritAlignment(float sin, float virtue)
		{
			return GetSpiritAlignment(GetAlignment(sin, virtue));
		}

		public static SpiritAlignment GetSpiritAlignment(float alignment)
		{
			if (alignment < 0.4f)
			{
				return SpiritAlignment.Sinner;
			}
			else if (alignment > 0.6f)
			{
				return SpiritAlignment.Saint;
			}
			else
			{
				return SpiritAlignment.Neutral;
			}
		}

		#endregion
	}
}
