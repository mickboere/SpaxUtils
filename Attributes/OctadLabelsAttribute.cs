using System;

namespace SpaxUtils
{
	/// <summary>
	/// Relabels an octad field's 8 members in the inspector, replacing the compass directions with what the lanes
	/// actually mean. Either name them directly, or point at a <see cref="StatOctadAsset"/> in a Resources folder
	/// and let the labels follow whatever that octad maps to.
	/// <para>Deliberately NOT a PropertyAttribute: Unity picks an attribute's drawer over a type's, which would
	/// displace the octad drawer. The octad drawer reads this off its fieldInfo instead.</para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class OctadLabelsAttribute : Attribute
	{
		/// <summary>Literal member names in octad order, or null when sourced from an asset.</summary>
		public string[] Labels { get; }

		/// <summary>Resources path to the <see cref="StatOctadAsset"/> naming the lanes, or null when literal.</summary>
		public string OctadResourcePath { get; }

		/// <param name="octadResourcePath">Resources path (no extension), e.g. "Stats/Octads/BodyPhysicsOctad".</param>
		public OctadLabelsAttribute(string octadResourcePath)
		{
			OctadResourcePath = octadResourcePath;
		}

		public OctadLabelsAttribute(string north, string northEast, string east, string southEast,
			string south, string southWest, string west, string northWest)
		{
			Labels = new[] { north, northEast, east, southEast, south, southWest, west, northWest };
		}
	}
}
