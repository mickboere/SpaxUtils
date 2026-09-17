using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Shader data for a smear: surfaces facing <see cref="Vector"/> near <see cref="Origin"/> are pulled along it,
	/// broken into scrolling streaks by <see cref="StreakAmount"/> and dithered by <see cref="Dither"/>.
	/// </summary>
	public struct DeformSmear
	{
		public Vector3 Vector;
		public Vector3 Origin;
		public float Falloff;
		public float Phase;
		/// <summary>Phase change per second; not a shader value, lets snapshots keep scrolling.</summary>
		public float Scroll;
		public float StreakScale;
		public float StreakAmount;
		public float Dither;
	}
}
