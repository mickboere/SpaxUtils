using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// The canonical element colors, in octad order (N, NE, E, SE, S, SW, W, NW).
	/// Single source of truth for anything that visualizes an octad.
	/// <para>Hues are the pure element colors lifted off their floor: blue barely carries luminance, so the
	/// unlifted Water and Void read as near-black on dark backgrounds. Every channel keeps enough headroom to
	/// stay legible as text or as a thin line, while the lift stays small enough that the hues remain distinct.</para>
	/// </summary>
	public static class ElementColors
	{
		public static readonly Color Fire = new(1f, 0.35f, 0.3f);
		public static readonly Color Light = new(1f, 1f, 0.35f);
		public static readonly Color Air = new(0.35f, 1f, 1f);
		public static readonly Color Spirit = new(1f, 0.4f, 1f);
		public static readonly Color Water = new(0.4f, 0.55f, 1f);
		public static readonly Color Nature = new(0.35f, 1f, 0.45f);
		public static readonly Color Earth = new(1f, 0.6f, 0.25f);
		public static readonly Color Void = new(0.7f, 0.45f, 1f);

		private static readonly Color[] octad = { Fire, Light, Air, Spirit, Water, Nature, Earth, Void };
		private static readonly string[] names = { "Fire", "Light", "Air", "Spirit", "Water", "Nature", "Earth", "Void" };

		/// <summary>Element color for octad <paramref name="index"/> (wraps).</summary>
		public static Color Get(int index) => octad[Wrap(index)];

		/// <summary>Element name for octad <paramref name="index"/> (wraps).</summary>
		public static string Name(int index) => names[Wrap(index)];

		/// <summary>Fresh copy of all 8 colors in octad order.</summary>
		public static Color[] ToArray() => (Color[])octad.Clone();

		private static int Wrap(int index) => ((index % 8) + 8) % 8;
	}
}
