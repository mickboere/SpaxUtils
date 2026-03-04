using UnityEngine;
using static RootMotion.FinalIK.Grounding;

namespace SpaxUtils
{
	/// <summary>
	/// Collection of standardized formulas used in Spirit Axis.
	/// </summary>
	public static class SpaxFormulas
	{
		public const float CONSTANT = 0.1f;
		public const float POWER = 2f;
		public const float SCALE = 100f;

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
			float exponent = 2f)
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
		/// Higher attackerPrecision increases coupling, higher defenderPliancy decreases it.
		/// </summary>
		/// <param name="precision">Attacker Precision (>= 0).</param>
		/// <param name="pliancy">Defender Pliancy (>= 0).</param>
		/// <param name="physicsPivot">Pivot constant for the contest (typically SCALE=100).</param>
		/// <returns>Coupling factor in 0..1.</returns>
		public static float CalculateCoupling(float precision, float pliancy, float physicsPivot = SCALE)
		{
			float pr = Mathf.Max(0f, precision);
			float pl = Mathf.Max(0f, pliancy);

			float coupling = pr / (pr + pl + physicsPivot);
			return Mathf.Clamp01(coupling);
		}

		/// <summary>
		/// Calculates the final critical-hit chance using a Precision vs Pliancy coupling contest,
		/// a smoothed Luck contest, and an anatomy-driven Vulnerability remap.
		/// Vulnerability anchors the output: 0 -> 0% crit, 0.5 -> base chance, 1 -> 100% crit.
		/// The returned value is clamped to 0..1.
		/// </summary>
		/// <param name="attackerPrecision">Attacker Precision (>= 0). Higher increases coupling.</param>
		/// <param name="defenderPliancy">Defender Pliancy (>= 0). Higher reduces coupling.</param>
		/// <param name="vulnerability01">
		/// Anatomy vulnerability in 0..1. Remaps chance so 0 disables crits, 0.5 leaves base chance unchanged,
		/// and 1 guarantees a crit.
		/// </param>
		/// <param name="attackerLuck">Attacker Luck (>= 0). Competes with defenderLuck in the luck contest.</param>
		/// <param name="defenderLuck">Defender Luck (>= 0). Competes with attackerLuck in the luck contest.</param>
		/// <param name="physicsPivot">
		/// Pivot constant for the coupling contest (typically SCALE=100). Higher pivots reduce sensitivity.
		/// </param>
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
