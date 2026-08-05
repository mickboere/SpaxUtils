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

		// Scale/shift constants for converting EXP levels to physics and pointstat values.
		public const float POINTSSTAT_SCALE = 10f;
		public const float POINTSSTAT_SHIFT = 100f;
		public const float PHYSIC_SCALE = 2f;
		public const float PHYSIC_SHIFT = 20f;

		// Relative equipment mass growth per rank (density fiction: same shape, denser material).
		// Also the rate at which exertion cost keeps pace with a growing Energy pool — gear is the only lane that does.
		public const float MASS_GROWTH = 0.04f;

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

			if (o <= 0.0001f) return 0f;

			float c = Mathf.Max(0f, crossFactor);
			float n = Mathf.Max(0.0001f, exponent);

			float ratio = d / o;
			float denom = 1f + c * Mathf.Pow(ratio, n);

			return o / denom;
		}

		/// <summary>
		/// Calculates the coupling factor (0..1) representing how cleanly a hit can couple into the target.
		/// Higher attacker Pierce increases coupling, higher defender Yield decreases it.
		/// </summary>
		/// <param name="pierce">Attacker Pierce (>= 0).</param>
		/// <param name="yield">Defender Yield (>= 0).</param>
		/// <param name="physicsPivot">Pivot constant for the contest (typically SCALE=100).</param>
		/// <returns>Coupling factor in 0..1.</returns>
		public static float CalculateCoupling(float pierce, float yield, float physicsPivot = SCALE)
		{
			float pi = Mathf.Max(0f, pierce);
			float yi = Mathf.Max(0f, yield);

			float coupling = pi / (pi + yi + physicsPivot);
			return Mathf.Clamp01(coupling);
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

		public static float LevelToPointsStat(float level, bool shift = true)
			=> level * POINTSSTAT_SCALE + (shift ? POINTSSTAT_SHIFT : 0f);

		public static float LevelToPhysic(float level, bool shift = true)
			=> level * PHYSIC_SCALE + (shift ? PHYSIC_SHIFT : 0f);

		/// <summary>
		/// Equipment's mass: the authored <paramref name="baseMass"/> grown relative to itself by rank, weighted
		/// by the physical-mass lanes only (N Power / W Armor) - other lanes (Slash/Pierce/Ward) add no mass.
		/// Rank-only by design - QUALITY makes gear better, not heavier.
		/// </summary>
		public static float EquipmentMass(float baseMass, float rank, Vector8 distribution)
			=> baseMass * (1f + MASS_GROWTH * rank * distribution[0].Max(distribution[6]));

		/// <summary>
		/// Per-lane weights for equipment's SHIFT: the distribution normalized to sum 1, scaled by the number
		/// of covered lanes. An evenly spread item therefore receives the full shift in each lane it covers,
		/// while a lopsided one concentrates its floor where its budget went.
		/// </summary>
		public static Vector8 EquipmentShiftWeights(Vector8 distribution)
		{
			GetRatioWeightsAndCoverage(distribution, out Vector8 ratioWeights, out _);

			int lanes = 0;
			for (int i = 0; i < 8; i++)
			{
				if (distribution[i] > 0f)
				{
					lanes++;
				}
			}

			return lanes > 0 ? ratioWeights * lanes : Vector8.Zero;
		}

		/// <summary>
		/// Equipment's physic contribution for a single lane, before coverage.
		/// <paramref name="level"/> already carries sqrt(QUALITY) through the points budget, so the shift gets
		/// the same sqrt treatment to keep QUALITY acting uniformly across both terms.
		/// <paramref name="shiftWeight"/> comes from <see cref="EquipmentShiftWeights"/>. The 4-lane geometry
		/// is itself the QUALITY 0.5 parity anchor: at n=4 and QUALITY 0.5 the effective level equals rank,
		/// matching the body's curve.
		/// </summary>
		public static float EquipmentPhysic(float level, float quality, float shiftWeight)
			=> level * PHYSIC_SCALE + PHYSIC_SHIFT * Mathf.Sqrt(Mathf.Max(0f, quality)) * Mathf.Max(0f, shiftWeight);

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
