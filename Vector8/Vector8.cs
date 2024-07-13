using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Data struct containing 8 float members, each defining a direction. Also known as an "Octon".
	/// </summary>
	[Serializable]
	public struct Vector8
	{
		public const float DIAGONAL = 0.70710856237f;

		#region Static Values
		public static Vector8 Zero => new Vector8(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
		public static Vector8 One => new Vector8(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);

		public static Vector8 North => new Vector8(1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
		public static Vector8 NorthEast => new Vector8(0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f);
		public static Vector8 East => new Vector8(0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f);
		public static Vector8 SouthEast => new Vector8(0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f);
		public static Vector8 South => new Vector8(0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f);
		public static Vector8 SouthWest => new Vector8(0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f);
		public static Vector8 West => new Vector8(0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f);
		public static Vector8 NorthWest => new Vector8(0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f);
		#endregion Static Values

		public float N, NE, E, SE, S, SW, W, NW;

		public float Min => Mathf.Min(N, NE, E, SE, S, SW, W, NW);
		public float Max => Mathf.Max(N, NE, E, SE, S, SW, W, NW);
		public float Sum => N + NE + E + SE + S + SW + W + NW;

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

		public Vector2[] GetPositions2D()
		{
			return new Vector2[8]
			{
				new Vector2(0f, N),
				new Vector2(NE, NE) * DIAGONAL,
				new Vector2(E, 0f),
				new Vector2(SE, -SE) * DIAGONAL,
				new Vector2(0f, -S),
				new Vector2(-SW, -SW) * DIAGONAL,
				new Vector2(-W, 0f),
				new Vector2(-NW, NW) * DIAGONAL
			};
		}

		public Vector3[] GetPositions3D()
		{
			return new Vector3[8]
			{
				new Vector2(0f, N),
				new Vector2(NE, NE) * DIAGONAL,
				new Vector2(E, 0f),
				new Vector2(SE, -SE) * DIAGONAL,
				new Vector2(0f, -S),
				new Vector2(-SW, -SW) * DIAGONAL,
				new Vector2(-W, 0f),
				new Vector2(-NW, NW) * DIAGONAL
			};
		}

		#region Static Functions

		/// <summary>
		/// Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> by <paramref name="t"/>.
		/// </summary>
		public static Vector8 Lerp(Vector8 a, Vector8 b, float t)
		{
			return new Vector8(
				a.N + (b.N - a.N) * t,
				a.NE + (b.NE - a.NE) * t,
				a.E + (b.E - a.E) * t,
				a.SE + (b.SE - a.SE) * t,
				a.S + (b.S - a.S) * t,
				a.SW + (b.SW - a.SW) * t,
				a.W + (b.W - a.W) * t,
				a.NW + (b.NW - a.NW) * t);
		}

		/// <summary>
		/// Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> by <paramref name="t"/>, clamping <paramref name="t"/> bewteen 0 and 1.
		/// </summary>
		public static Vector8 LerpClamped(Vector8 a, Vector8 b, float t)
		{
			return Lerp(a, b, Mathf.Clamp01(t));
		}

		/// <summary>
		/// Rotates <paramref name="v"/> by <paramref name="amount"/> members.
		/// </summary>
		/// <param name="v">The <see cref="Vector8"/> to rotate.</param>
		/// <param name="amount">The amount of members to rotate by (8 is a full rotation).</param>
		/// <returns><paramref name="v"/> rotated by <paramref name="amount"/> members.</returns>
		public static Vector8 Rotate(Vector8 v, int amount)
		{
			return new Vector8(
				v[amount % 8],
				v[(amount + 1) % 8],
				v[(amount + 2) % 8],
				v[(amount + 3) % 8],
				v[(amount + 4) % 8],
				v[(amount + 5) % 8],
				v[(amount + 6) % 8],
				v[(amount + 7) % 8]);
		}

		/// <summary>
		/// Travels from <paramref name="a"/> to <paramref name="b"/> taking steps of <paramref name="t"/>, never exceeding <paramref name="b"/>.
		/// </summary>
		public static Vector8 Travel(Vector8 a, Vector8 b, float t)
		{
			return new Vector8(
				T(a.N, b.N, t),
				T(a.NE, b.NE, t),
				T(a.E, b.E, t),
				T(a.SE, b.SE, t),
				T(a.S, b.S, t),
				T(a.SW, b.SW, t),
				T(a.W, b.W, t),
				T(a.NW, b.NW, t));

			float T(float a, float b, float t)
			{
				float d = Mathf.Sign(b - a);
				if (d < 0)
				{
					return Mathf.Max(b, a - t);
				}
				else
				{
					return Mathf.Min(b, a + t);
				}
			}
		}

		/// <summary>
		/// Perform a "fluid" simulation on Vector8 <paramref name="v"/> where each member flows into its neighbours' weights <paramref name="w"/> multiplied by <paramref name="t"/>.
		/// </summary>
		/// <param name="v">The vector to perform the simulation on.</param>
		/// <param name="r">The rest position of the simulation.</param>
		/// <param name="w">The vector containing the simulation weights.</param>
		/// <param name="t">The "timestep".</param>
		/// <returns><paramref name="v"/> with a flow simulation applied.</returns>
		public static Vector8 Simulate(Vector8 v, Vector8 r, Vector8 w, float t)
		{
			Vector8 s = v;
			int iP, iN;
			float vP, vN, f, d;
			for (int i = 0; i < 8; i++)
			{
				// Calculate neighbour indices.
				iP = Mod(i - 1, 8);
				iN = Mod(i + 1, 8);
				// Calculate flow values.
				f = 1f / (w[i] + w[iP] + w[iN]);
				d = Mathf.Max(0f, v[i] - r[i]);
				vP = d * w[iP] * f * t;
				vN = d * w[iN] * f * t;
				// Apply flow values.
				s[i] -= vP + vN;
				s[iP] += vP;
				s[iN] += vN;
			}
			return s;

			int Mod(int x, int m)
			{
				int r = x % m;
				return r < 0 ? r + m : r;
			}
		}

		#endregion

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
						throw new ArgumentOutOfRangeException("index", $"Vector8 index needs to be between 0 and 7, but ({index}) was given!");
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
						throw new ArgumentOutOfRangeException("index", "Vector8 index needs to be between 0 and 7, but ({index}) was given!");
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
