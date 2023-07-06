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
			float E()
			{
				return Mathf.Pow(level / constant, power);
			}
			return round ? Mathf.Round(E()) : E();
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
			float ED()
			{
				return Exp(level, constant, power, implicitRound) - Exp(level - 1, constant, power, implicitRound);
			}
			return round ? Mathf.Round(ED()) : ED();
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
			float E()
			{
				return Mathf.Pow(exp, 1f / power) * constant;
			}
			return floor ? Mathf.Floor(E()) : E();
		}

		/// <summary>
		/// Standardized logarithmic formula used for converting LEVELS to POINTS.
		/// </summary>
		/// <param name="level">The level to calculate the STAT POINTS for.</param>
		/// <param name="constant">The x constant modifier.</param>
		/// <param name="power">The y power modifier.</param>
		/// <param name="scale">The y scale modifier.</param>
		/// <param name="shift">The y shift modifier.</param>
		/// <param name="round">Whether the result should be rounded to an integer.</param>
		/// <returns><paramref name="level"/> converted to POINTS.</returns>
		public static float Log(float level, float constant = CONSTANT, float power = POWER, float scale = SCALE, float shift = 0f,
			bool round = false)
		{
			float L()
			{
				return shift + Mathf.Log(1f + level / constant, power) * scale;
			}
			return round ? Mathf.Round(L()) : L();
		}

		/// <summary>
		/// Returns the POINTS difference from the previous level to the current <paramref name="level"/>.
		/// </summary>
		/// <param name="level">The level to calculate the POINT jump for.</param>
		/// <param name="constant">The x constant modifier.</param>
		/// <param name="power">The y power modifier.</param>
		/// <param name="scale">The y scale modifier.</param>
		/// <param name="shift">The y shift modifier.</param>
		/// <param name="round">Whether the result should be rounded to an integer.</param>
		/// <param name="implicitRound">Whether the individuals Log results should be rounded as well.</param>
		/// <returns>The POINTS difference from the previous level to the current <paramref name="level"/>.</returns>
		public static float LogDiff(float level, float constant = CONSTANT, float power = POWER, float scale = SCALE, float shift = 0f,
			bool round = false, bool implicitRound = false)
		{
			float LD()
			{
				return Log(level, constant, power, scale, shift, implicitRound) - Log(level - 1f, constant, power, scale, shift, implicitRound);
			}
			return round ? Mathf.Round(LD()) : LD();
		}

		/// <summary>
		/// Calculate damage using strength, offense and defense.
		/// </summary>
		/// <param name="strength">The strength of the "attack". In melee combat this would be STR * (Attack Move Strength + Weapon Strength)
		/// <para>When the strength value does not apply it can be the same as the offense value.</para></param>
		/// <param name="offense">The offense stat of the attacker.</param>
		/// <param name="defense">The defense stat of the defender.</param>
		/// <returns>The dealt damage.</returns>
		public static float GetDamage(float strength, float offense, float defense)
		{
			return (offense * strength) / (defense + 100f);
		}

		/// <summary>
		/// Calculate damage using offense and defense.
		/// </summary>
		/// <param name="offense">The offense stat of the attacker.</param>
		/// <param name="defense">The defense stat of the defender.</param>
		/// <returns>The dealt damage.</returns>
		public static float GetDamage(float offense, float defense)
		{
			return GetDamage(offense, offense, defense);
		}
	}
}
