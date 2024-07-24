using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that behaves as a <see cref="SpaxUtils.Vector8"/>, assigning a ranged float (0-1) to each member.
	/// </summary>
	[Serializable]
	public class RangedOcton : IOcton
	{
		public Vector8 Vector8 => new Vector8(north, northEast, east, southEast, south, southWest, west, northWest);

		[SerializeField, Range(0f, 1f)] private float north = 0.5f;
		[SerializeField, Range(0f, 1f)] private float northEast = 0.5f;
		[SerializeField, Range(0f, 1f)] private float east = 0.5f;
		[SerializeField, Range(0f, 1f)] private float southEast = 0.5f;
		[SerializeField, Range(0f, 1f)] private float south = 0.5f;
		[SerializeField, Range(0f, 1f)] private float southWest = 0.5f;
		[SerializeField, Range(0f, 1f)] private float west = 0.5f;
		[SerializeField, Range(0f, 1f)] private float northWest = 0.5f;

		public RangedOcton(float fire, float light, float air, float faeth, float water, float nature, float earth, float daeth)
		{
			this.north = fire;
			this.northEast = light;
			this.east = air;
			this.southEast = faeth;
			this.south = water;
			this.southWest = nature;
			this.west = earth;
			this.northWest = daeth;
		}

		public static implicit operator Vector8(RangedOcton octon)
		{
			return octon.Vector8;
		}

		public override string ToString()
		{
			return Vector8.ToString();
		}

		public string ToStringShort()
		{
			return Vector8.ToStringShort();
		}
	}
}
