using UnityEngine;

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

		/// <summary>
		/// The default tweaked logarithmic formula used for converting LEVELS to POINTS.
		/// </summary>
		/// <param name="level">The input level to convert into points.</param>
		/// <param name="xScale">The x scale modifier.</param>
		/// <param name="yScale">The value of y at <paramref name="xScale"/>.</param>
		/// <param name="round">Whether to round the points to whole numbers.</param>
		//public static float DefaultLevelToPoints(float level, float xScale = 100f, float yScale = 1000f, bool round = false)
		//{
		//	return Log(level, xScale / 9f, 10f, yScale, 0f, round);
		//}

		/// <summary>
		/// Standard damage formula taking an offence value and separate protection and defence values.
		/// Protection is a linear subtractor, defence (poise) defines the curved part.
		/// </summary>
		/// <param name="offence">The attacker's offensive power.</param>
		/// <param name="protection">The defender's linear protection.</param>
		/// <param name="defence">The defender's curved defence (poise/hardness).</param>
		/// <param name="exponence">
		/// Amount of exponence applied to the formula.
		/// 0 will simply subtract protection+defence linearly (afterProtection),
		/// 1 will fully use the curved term.
		/// </param>
		public static float CalculateDamage(float offence, float protection, float defence, float exponence = 0.5f, bool round = false)
		{
			// Safety clamps to prevent negative math
			float safeDefence = Mathf.Max(0f, defence);
			float safeProtection = Mathf.Max(0f, protection);
			float safeOffence = Mathf.Max(0f, offence);

			// 1. Linear Term: "The Armor Check"
			// If Protection >= Offence, this is 0.
			float linear = Mathf.Max(0f, safeOffence - safeProtection);

			// 2. Curved Term: "The Mitigation Check"
			// Uses the system constant SCALE (100) as the pivot.
			// Example: 100 Offence vs 100 Defence = 100 * (100/200) = 50 Damage.
			float mitigationFactor = SCALE / (SCALE + safeDefence);
			float expo = safeOffence * mitigationFactor;

			// Blend based on slider
			float value = Mathf.Lerp(linear, expo, exponence);
			return round ? Mathf.Round(value) : value;
		}

		#region Curves

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

		#endregion Curves
	}
}
