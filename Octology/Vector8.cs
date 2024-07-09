using System;

namespace SpaxUtils
{
	/// <summary>
	/// Data struct containing 8 float members, each defining a direction. Also known as an "Octon".
	/// </summary>
	[Serializable]
	public struct Vector8
	{
		public float N, NE, E, SE, S, SW, W, NW;

		public Vector8(float north, float northEast, float east, float southEast, float south, float southWest, float west, float northWest)
		{
			N = north;
			NE = northEast;
			E = east;
			SE = southEast;
			S = south;
			SW = southWest;
			W = west;
			NW = northWest;
		}

		public Vector8(float[] array)
		{
			N = array[0];
			NE = array[1];
			E = array[2];
			SE = array[3];
			S = array[4];
			SW = array[5];
			W = array[6];
			NW = array[7];
		}

		/// <summary>
		/// The sum of all 8 members.
		/// </summary>
		public float Sum
		{
			get
			{
				return N + NE + E + SE + S + SW + W + NW;
			}
		}

		#region Arrays

		/// <summary>
		/// Access the Vector8 members by index, with 0 starting at NORTH, going clockwise.
		/// </summary>
		/// <param name="index">The index of the member to access with 0 starting at NORTH, going clockwise.</param>
		/// <returns>The value of the member corresponding to <paramref name="index"/></returns>.
		public float this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return N;
					case 1: return NE;
					case 2: return E;
					case 3: return SE;
					case 4: return S;
					case 5: return SW;
					case 6: return W;
					case 7: return NW;
					default:
						throw new ArgumentOutOfRangeException("index", "Vector8 index needs to be between 0 and 7.");
				}
			}
			set
			{
				switch (index)
				{
					case 0: N = value; break;
					case 1: NE = value; break;
					case 2: E = value; break;
					case 3: SE = value; break;
					case 4: S = value; break;
					case 5: SW = value; break;
					case 6: W = value; break;
					case 7: NW = value; break;
					default:
						throw new ArgumentOutOfRangeException("index", "Vector8 index needs to be between 0 and 7.");
				}
			}
		}

		/// <summary>
		/// Convert the Vector8 into an array.
		/// </summary>
		/// <returns>A float array of the Vector8's members.</returns>
		public float[] ToArray()
		{
			return new float[8] { N, NE, E, SE, S, SW, W, NW };
		}

		/// <summary>
		/// Implicit <see cref="ToArray"/> operator.
		/// </summary>
		public static implicit operator float[](Vector8 vector)
		{
			return vector.ToArray();
		}

		#endregion Arrays

		#region Operators

		/// <summary>
		/// Adds <paramref name="a"/> and <paramref name="b"/> together, resulting in a new <see cref="Vector8"/>.
		/// </summary>
		public static Vector8 operator +(Vector8 a, Vector8 b)
		{
			return new Vector8(
				a.N + b.N,
				a.NE + b.NE,
				a.E + b.E,
				a.SE + b.SE,
				a.S + b.S,
				a.SW + b.SW,
				a.W + b.W,
				a.NW + b.NW);
		}

		/// <summary>
		/// Substracts <paramref name="a"/> from <paramref name="b"/>, resulting in a new <see cref="Vector8"/>.
		/// </summary>
		public static Vector8 operator -(Vector8 a, Vector8 b)
		{
			return new Vector8(
				a.N - b.N,
				a.NE - b.NE,
				a.E - b.E,
				a.SE - b.SE,
				a.S - b.S,
				a.SW - b.SW,
				a.W - b.W,
				a.NW - b.NW);
		}

		/// <summary>
		/// Multiplies <paramref name="a"/> with <paramref name="b"/>, resulting in a new <see cref="Vector8"/>.
		/// </summary>
		public static Vector8 operator *(Vector8 a, Vector8 b)
		{
			return new Vector8(
				a.N * b.N,
				a.NE * b.NE,
				a.E * b.E,
				a.SE * b.SE,
				a.S * b.S,
				a.SW * b.SW,
				a.W * b.W,
				a.NW * b.NW);
		}

		/// <summary>
		/// Divides <paramref name="a"/> by <paramref name="b"/>, resulting in a new <see cref="Vector8"/>.
		/// </summary>
		public static Vector8 operator /(Vector8 a, Vector8 b)
		{
			return new Vector8(
				a.N / b.N,
				a.NE / b.NE,
				a.E / b.E,
				a.SE / b.SE,
				a.S / b.S,
				a.SW / b.SW,
				a.W / b.W,
				a.NW / b.NW);
		}

		/// <summary>
		/// Multiplies all members of <paramref name="a"/> by <paramref name="b"/>, resulting in a new <see cref="Vector8"/>.
		/// </summary>
		public static Vector8 operator *(Vector8 a, float b)
		{
			return new Vector8(
				a.N * b,
				a.NE * b,
				a.E * b,
				a.SE * b,
				a.S * b,
				a.SW * b,
				a.W * b,
				a.NW * b);
		}

		/// <summary>
		/// Divides all members of <paramref name="a"/> by <paramref name="b"/>, resulting in a new <see cref="Vector8"/>.
		/// </summary>
		public static Vector8 operator /(Vector8 a, float b)
		{
			return new Vector8(
				a.N / b,
				a.NE / b,
				a.E / b,
				a.SE / b,
				a.S / b,
				a.SW / b,
				a.W / b,
				a.NW / b);
		}

		#endregion Operators

		public override string ToString()
		{
			return $"(N={N}, NE={NE}, E={E}, SE={SE}, S={S}, SW={SW}, W={W}, NW={NW})";
		}

		public string ToStringShort()
		{
			return $"({N}, {NE}, {E}, {SE}, {S}, {SW}, {W}, {NW})";
		}
	}
}
