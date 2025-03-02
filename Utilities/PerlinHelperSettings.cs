using UnityEngine;
using System;

namespace SpaxUtils
{
	[Serializable]
	public class PerlinHelperSettings
	{
		[SerializeField, MinMaxRange(1f, 10f), Tooltip("Strafing perlin-noise intensity. Higher = less standing still / prolonged strafing.")] public Vector2 Polarization = new Vector2(1f, 10f);
		[SerializeField, MinMaxRange(0.1f, 1f), Tooltip("Strafing perlin-noise frequency. Higher = more frequent changes in direction.")] public Vector2 Frequency = new Vector2(0.1f, 1f);
	}
}
