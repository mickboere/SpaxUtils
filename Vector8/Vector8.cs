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
		public static Vector8 Half => new Vector8(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);

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

		#region Public Functions

		/// <summary>
		/// Returns the smallest value of all members.
		/// </summary>
		public float Min() => Min(this);

		/// <summary>
		/// Returns the highest value of all members.
		/// </summary>
		/// <returns></returns>
		public float Max() => Max(this);

		/// <summary>
		/// Returns the sum of all members.
		/// </summary>
		public float Sum() => Sum(this);

		/// <summary>
		/// Returns the value of the highest of the 8 members.
		/// </summary>
		/// <param name="index">The index of the highest member.</param>
		/// <returns>The value of the highest of the 8 members.</returns>
		public float Highest(out int index) => Highest(this, out index);

		/// <summary>
		/// Linearly interpolates to <paramref name="b"/> by <paramref name="t"/>.
		/// </summary>
		public Vector8 Lerp(Vector8 b, float t) => Lerp(this, b, t);

		/// <summary>
		/// Linearly interpolates to <paramref name="b"/> by <paramref name="t"/>, clamping <paramref name="t"/> bewteen 0 and 1.
		/// </summary>
		public Vector8 LerpClamped(Vector8 b, float t) => LerpClamped(this, b, t);

		/// <summary>
		/// Shifts member values clockwise by <paramref name="amount"/> members.
		/// </summary>
		/// <param name="amount">The amount of members to rotate by (8 is a full rotation).</param>
		/// <returns>This Vector8 rotated by <paramref name="amount"/> members.</returns>
		public Vector8 Rotate(int amount) => Rotate(this, amount);

		/// <summary>
		/// Travels to <paramref name="b"/> taking steps of <paramref name="t"/>, never exceeding <paramref name="b"/>.
		/// </summary>
		public Vector8 Travel(Vector8 b, float t) => Travel(this, b, t);

		/// <summary>
		/// Clamps the member values between <paramref name="min"/> and <paramref name="max"/>.
		/// </summary>
		public Vector8 Clamp(float min, float max) => Clamp(this, min, max);

		/// <summary>
		/// Clamps the members values between 0 and 1.
		/// </summary>
		public Vector8 Clamp01() => Clamp01(this);

		/// <summary>
		/// Scales all members values proportionally so that its highest member never exceeds 1.
		/// Does NOT make it so that the total length of the vector is 1!
		/// </summary>
		public Vector8 Normalize() => Normalize(this);

		/// <summary>
		/// Turns all member values into absolute values (turns all negatives into positives).
		/// </summary>
		public Vector8 Absolute() => Absolute(this);

		/// <summary>
		/// Returns the total distance to <paramref name="b"/> (sum of all member distances).
		/// </summary>
		public float Distance(Vector8 b) => Distance(this, b);

		/// <summary>
		/// Disperses the member values into their neighbours using <paramref name="w"/> as weights multiplied by <paramref name="t"/>.
		/// </summary>
		/// <param name="r">The rest position of the simulation.</param>
		/// <param name="w">The vector containing the simulation weights.</param>
		/// <param name="t">The "timestep".</param>
		/// <returns><paramref name="v"/> with a flow simulation applied.</returns>
		public Vector8 Disperse(Vector8 r, Vector8 w, float t) => Disperse(this, r, w, t);

		/// <summary>
		/// Get the positions of all members arranged in a circle.
		/// </summary>
		/// <returns>An array of <see cref="Vector2"/> positions.</returns>
		public Vector2[] GetPositions2D() => GetPositions2D(this);

		/// <summary>
		/// Get the positions of all members arranged in a circle.
		/// </summary>
		/// <returns>An array of <see cref="Vector3"/> positions where Z is 0.</returns>
		public Vector3[] GetPositions3D() => GetPositions3D(this);

		#endregion Public Functions

		#region Static Functions

		/// <summary>
		/// Returns the smallest value of all members.
		/// </summary>
		public static float Min(Vector8 v) => Mathf.Min(v.N, v.NE, v.E, v.SE, v.S, v.SW, v.W, v.NW);

		/// <summary>
		/// Returns the highest value of all members.
		/// </summary>
		/// <returns></returns>
		public static float Max(Vector8 v) => Mathf.Max(v.N, v.NE, v.E, v.SE, v.S, v.SW, v.W, v.NW);

		/// <summary>
		/// Returns the sum of all members.
		/// </summary>
		public static float Sum(Vector8 v) => v.N + v.NE + v.E + v.SE + v.S + v.SW + v.W + v.NW;

		/// <summary>
		/// Returns the value of the highest of the 8 members.
		/// </summary>
		/// <param name="index">The index of the highest member.</param>
		/// <returns>The value of the highest of the 8 members.</returns>
		public static float Highest(Vector8 v, out int index)
		{
			float max = 0f;
			index = -1;
			for (int i = 0; i < 8; i++)
			{
				if (v[i] > max)
				{
					max = v[i];
					index = i;
				}
			}
			return max;
		}

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
		/// Clamps the members of <paramref name="v"/> between <paramref name="min"/> and <paramref name="max"/>.
		/// </summary>
		public static Vector8 Clamp(Vector8 v, float min, float max)
		{
			return new Vector8(
				Mathf.Clamp(v.N, min, max),
				Mathf.Clamp(v.NE, min, max),
				Mathf.Clamp(v.E, min, max),
				Mathf.Clamp(v.SE, min, max),
				Mathf.Clamp(v.S, min, max),
				Mathf.Clamp(v.SW, min, max),
				Mathf.Clamp(v.W, min, max),
				Mathf.Clamp(v.NW, min, max));
		}

		/// <summary>
		/// Clamps the members of <paramref name="v"/> between 0 and 1.
		/// </summary>
		public static Vector8 Clamp01(Vector8 v)
		{
			return new Vector8(
				Mathf.Clamp01(v.N),
				Mathf.Clamp01(v.NE),
				Mathf.Clamp01(v.E),
				Mathf.Clamp01(v.SE),
				Mathf.Clamp01(v.S),
				Mathf.Clamp01(v.SW),
				Mathf.Clamp01(v.W),
				Mathf.Clamp01(v.NW));
		}

		/// <summary>
		/// Scales all of <paramref name="v"/>'s members proportionally so that its highest member never exceeds 1.
		/// Does NOT make it so that the total length of the vector is 1!
		/// </summary>
		public static Vector8 Normalize(Vector8 v)
		{
			if (v.Max() <= 0f)
			{
				return v;
			}
			float m = 1f / v.Max();
			return v * m;
		}

		/// <summary>
		/// Makes all of <paramref name="v"/>'s members absolute values (turns all negatives into positives).
		/// </summary>
		public static Vector8 Absolute(Vector8 v)
		{
			return new Vector8(
				Mathf.Abs(v.N),
				Mathf.Abs(v.NE),
				Mathf.Abs(v.E),
				Mathf.Abs(v.SE),
				Mathf.Abs(v.S),
				Mathf.Abs(v.SW),
				Mathf.Abs(v.W),
				Mathf.Abs(v.NW));
		}

		/// <summary>
		/// Returns the total distance between vectors <paramref name="a"/> and <paramref name="b"/>.
		/// </summary>
		public static float Distance(Vector8 a, Vector8 b)
		{
			return (b - a).Absolute().Sum();
		}

		/// <summary>
		/// Disperses members of <paramref name="v"/> into their neighbours using <paramref name="w"/> as weights, multiplied by <paramref name="t"/>.
		/// </summary>
		/// <param name="v">The vector to perform the simulation on.</param>
		/// <param name="r">The rest position of the simulation.</param>
		/// <param name="w">The vector containing the simulation weights.</param>
		/// <param name="t">The "timestep".</param>
		/// <returns><paramref name="v"/> with a flow simulation applied.</returns>
		public static Vector8 Disperse(Vector8 v, Vector8 r, Vector8 w, float t)
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

		/// <summary>
		/// Get the positions of all members arranged in a circle.
		/// </summary>
		/// <returns>An array of <see cref="Vector2"/> positions.</returns>
		public static Vector2[] GetPositions2D(Vector8 v)
		{
			return new Vector2[8]
			{
				new Vector2(0f, v.N),
				new Vector2(v.NE, v.NE) * DIAGONAL,
				new Vector2(v.E, 0f),
				new Vector2(v.SE, -v.SE) * DIAGONAL,
				new Vector2(0f, -v.S),
				new Vector2(-v.SW, -v.SW) * DIAGONAL,
				new Vector2(-v.W, 0f),
				new Vector2(-v.NW, v.NW) * DIAGONAL
			};
		}

		/// <summary>
		/// Get the positions of all members arranged in a circle.
		/// </summary>
		/// <returns>An array of <see cref="Vector3"/> positions where Z is 0.</returns>
		public static Vector3[] GetPositions3D(Vector8 v)
		{
			return new Vector3[8]
			{
				new Vector2(0f, v.N),
				new Vector2(v.NE, v.NE) * DIAGONAL,
				new Vector2(v.E, 0f),
				new Vector2(v.SE, -v.SE) * DIAGONAL,
				new Vector2(0f, -v.S),
				new Vector2(-v.SW, -v.SW) * DIAGONAL,
				new Vector2(-v.W, 0f),
				new Vector2(-v.NW, v.NW) * DIAGONAL
			};
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
		/// Operand negative operator for <see cref="Vector8"/> <paramref name="a"/>.
		/// </summary>
		public static Vector8 operator -(Vector8 a)
		{
			return new Vector8(-a.N, -a.NE, -a.E, -a.SE, -a.S, -a.SW, -a.W, -a.NW);
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

		/// <summary>
		/// Returns whether all members of <paramref name="a"/> exceed their counterparts in <paramref name="b"/>.
		/// </summary>
		public static bool operator >(Vector8 a, Vector8 b)
		{
			return
				a.N > b.N &&
				a.NE > b.NE &&
				a.E > b.E &&
				a.SE > b.SE &&
				a.S > b.S &&
				a.SW > b.SW &&
				a.W > b.W &&
				a.NW > b.NW;
		}

		/// <summary>
		/// Returns whether all members of <paramref name="a"/> either match or exceed their counterparts in <paramref name="b"/>.
		/// </summary>
		public static bool operator >=(Vector8 a, Vector8 b)
		{
			return
				a.N >= b.N &&
				a.NE >= b.NE &&
				a.E >= b.E &&
				a.SE >= b.SE &&
				a.S >= b.S &&
				a.SW >= b.SW &&
				a.W >= b.W &&
				a.NW >= b.NW;
		}

		/// <summary>
		/// Returns whether all members of <paramref name="a"/> are smaller than their counterparts in <paramref name="b"/>.
		/// </summary>
		public static bool operator <(Vector8 a, Vector8 b)
		{
			return
				a.N < b.N &&
				a.NE < b.NE &&
				a.E < b.E &&
				a.SE < b.SE &&
				a.S < b.S &&
				a.SW < b.SW &&
				a.W < b.W &&
				a.NW < b.NW;
		}

		/// <summary>
		/// Returns whether all members of <paramref name="a"/> either match or are smaller than their counterparts in <paramref name="b"/>.
		/// </summary>
		public static bool operator <=(Vector8 a, Vector8 b)
		{
			return
				a.N <= b.N &&
				a.NE <= b.NE &&
				a.E <= b.E &&
				a.SE <= b.SE &&
				a.S <= b.S &&
				a.SW <= b.SW &&
				a.W <= b.W &&
				a.NW <= b.NW;
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
