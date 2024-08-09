using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class PointStatOcton : IOcton
	{
		public Vector8 Vector8 => Current;
		public Vector8 Current => new Vector8(N, NE, E, SE, S, SW, W, NW);
		public Vector8 Max => new Vector8(N.Max, NE.Max, E.Max, SE.Max, S.Max, SW.Max, W.Max, NW.Max);
		public Vector8 Recoverable => new Vector8(N.Recoverable, NE.Recoverable, E.Recoverable, SE.Recoverable, S.Recoverable, SW.Recoverable, W.Recoverable, NW.Recoverable);

		public PointsStat N => north;
		[SerializeField] private PointsStat north;
		public PointsStat NE => northEast;
		[SerializeField] private PointsStat northEast;
		public PointsStat E => east;
		[SerializeField] private PointsStat east;
		public PointsStat SE => southEast;
		[SerializeField] private PointsStat southEast;
		public PointsStat S => south;
		[SerializeField] private PointsStat south;
		public PointsStat SW => southWest;
		[SerializeField] private PointsStat southWest;
		public PointsStat W => west;
		[SerializeField] private PointsStat west;
		public PointsStat NW => northWest;
		[SerializeField] private PointsStat northWest;

		public void Initialize(IEntity entity)
		{
			for (int i = 0; i < 8; i++)
			{
				this[i].Initialize(entity);
			}
		}

		public void Update(float delta)
		{
			for (int i = 0; i < 8; i++)
			{
				this[i].Update(delta);
			}
		}

		public void Recover()
		{
			for (int i = 0; i < 8; i++)
			{
				this[i].Recover();
			}
		}

		/// <summary>
		/// Implicit <see cref="SpaxUtils.Vector8"/> conversion.
		/// </summary>
		public static implicit operator Vector8(PointStatOcton octon)
		{
			return octon.Current;
		}

		/// <summary>
		/// Access the octon members by index, with 0 starting at NORTH, going clockwise.
		/// </summary>
		/// <param name="index">The index of the member to access with 0 starting at NORTH, going clockwise.</param>
		/// <returns>The value of the member corresponding to <paramref name="index"/></returns>.
		public PointsStat this[int index]
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
		}

		public override string ToString()
		{
			return $"({M("N", N)}, {M("NE", NE)}, {M("E", E)}, {M("SE", SE)}, {M("S", S)}, {M("SW", SW)}, {M("W", W)}, {M("NW", NW)})";

			string M(string heading, PointsStat stat)
			{
				return $"\"{stat.Identifier}\"({heading})={stat.Current.Value}";
			}
		}

		public string ToStringShort()
		{
			return $"({N.Current.Value}, {NE.Current.Value}, {E.Current.Value}, {SE.Current.Value}, {S.Current.Value}, {SW.Current.Value}, {W.Current.Value}, {NW.Current.Value})";
		}
	}
}
