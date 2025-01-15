using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SpaxUtils
{
	/// <summary>
	/// Octad (that does not implement <see cref="IOctad"/>) which only contains min-max ranges for each attribute.
	/// To retrieve a <see cref="Vector8"/> from this object requires an interpolator which evaluates the ranges.
	/// </summary>
	[Serializable]
	public class MinMaxOctad
	{
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 north;
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 northEast;
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 east;
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 southEast;
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 south;
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 southWest;
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 west;
		[SerializeField, MinMaxRange(0f, 1f)] private Vector2 northWest;

		public MinMaxOctad(Vector2 north, Vector2 northEast, Vector2 east, Vector2 southEast,
			Vector2 south, Vector2 southWest, Vector2 west, Vector2 northWest)
		{
			this.north = north;
			this.northEast = northEast;
			this.east = east;
			this.southEast = southEast;
			this.south = south;
			this.southWest = southWest;
			this.west = west;
			this.northWest = northWest;
		}

		/// <summary>
		/// Will interpolate the value ranges from their min to their max using <paramref name="f"/>.
		/// </summary>
		/// <param name="f">The linear interpolation value (0-1).</param>
		/// <returns>All value ranges interpolated from their min to their max by <paramref name="f"/>.</returns>
		public Vector8 Interpolate(float f)
		{
			return new Vector8(
				Mathf.Lerp(north.x, north.y, f),
				Mathf.Lerp(northEast.x, northEast.y, f),
				Mathf.Lerp(east.x, east.y, f),
				Mathf.Lerp(southEast.x, southEast.y, f),
				Mathf.Lerp(south.x, south.y, f),
				Mathf.Lerp(southWest.x, southWest.y, f),
				Mathf.Lerp(west.x, west.y, f),
				Mathf.Lerp(northWest.x, northWest.y, f));
		}

		/// <summary>
		/// Will interpolate the value ranges from their min to their max using the provided <paramref name="v8"/>'s corresponding values.
		/// </summary>
		/// <param name="v8">The linear interpolation values (8x0-1).</param>
		/// <returns>All value ranges interpolated from their min to their max by <paramref name="v8"/>.</returns>
		public Vector8 Interpolate(Vector8 v8)
		{
			return new Vector8(
				Mathf.Lerp(north.x, north.y, v8.N),
				Mathf.Lerp(northEast.x, northEast.y, v8.NE),
				Mathf.Lerp(east.x, east.y, v8.E),
				Mathf.Lerp(southEast.x, southEast.y, v8.SE),
				Mathf.Lerp(south.x, south.y, v8.S),
				Mathf.Lerp(southWest.x, southWest.y, v8.SW),
				Mathf.Lerp(west.x, west.y, v8.W),
				Mathf.Lerp(northWest.x, northWest.y, v8.NW));
		}

		/// <summary>
		/// Will return a randomized <see cref="Vector8"/> ranging between this class' min/max values.
		/// </summary>
		/// <param name="seed">The seed to initialize the randomness with.</param>
		/// <returns>A randomized <see cref="Vector8"/> ranging between this class' min/max values.</returns>
		public Vector8 Randomize(int seed)
		{
			Random.InitState(seed);
			return new Vector8(
				Random.Range(north.x, north.y),
				Random.Range(northEast.x, northEast.y),
				Random.Range(east.x, east.y),
				Random.Range(southEast.x, southEast.y),
				Random.Range(south.x, south.y),
				Random.Range(southWest.x, southWest.y),
				Random.Range(west.x, west.y),
				Random.Range(northWest.x, northWest.y));
		}
	}
}
