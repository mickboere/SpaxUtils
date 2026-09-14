using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Octad implementation that manages 8 <see cref="ResourceStat"/>s, meaning 8 stats that have a max- and recoverable current value.
	/// </summary>
	[Serializable]
	public class ResourceStatOctad : IOctad
	{
		public Vector8 Vector8 => Current;
		public Vector8 Current => new Vector8(N, NE, E, SE, S, SW, W, NW);
		public Vector8 Max => new Vector8(N.Max ?? 0f, NE.Max ?? 0f, E.Max ?? 0f, SE.Max ?? 0f, S.Max ?? 0f, SW.Max ?? 0f, W.Max ?? 0f, NW.Max ?? 0f);
		public Vector8 Recoverable => new Vector8(N.Reserve ?? 0f, NE.Reserve ?? 0f, E.Reserve ?? 0f, SE.Reserve ?? 0f, S.Reserve ?? 0f, SW.Reserve ?? 0f, W.Reserve ?? 0f, NW.Reserve ?? 0f);

		public ResourceStat N => north;
		[SerializeField] private ResourceStat north;
		public ResourceStat NE => northEast;
		[SerializeField] private ResourceStat northEast;
		public ResourceStat E => east;
		[SerializeField] private ResourceStat east;
		public ResourceStat SE => southEast;
		[SerializeField] private ResourceStat southEast;
		public ResourceStat S => south;
		[SerializeField] private ResourceStat south;
		public ResourceStat SW => southWest;
		[SerializeField] private ResourceStat southWest;
		public ResourceStat W => west;
		[SerializeField] private ResourceStat west;
		public ResourceStat NW => northWest;
		[SerializeField] private ResourceStat northWest;

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

		/// <summary>
		/// Recovers all octad members that have <see cref="ResourceStat.DefaultIsFull"/> set to true.
		/// </summary>
		public void Recover()
		{
			for (int i = 0; i < 8; i++)
			{
				if (this[i].DefaultIsFull)
				{
					this[i].Recover();
				}
			}
		}

		/// <summary>
		/// Implicit <see cref="SpaxUtils.Vector8"/> conversion.
		/// </summary>
		public static implicit operator Vector8(ResourceStatOctad octon)
		{
			return octon.Current;
		}

		/// <summary>
		/// Access the octon members by index, with 0 starting at NORTH, going clockwise.
		/// </summary>
		/// <param name="index">The index of the member to access with 0 starting at NORTH, going clockwise.</param>
		/// <returns>The value of the member corresponding to <paramref name="index"/></returns>.
		public ResourceStat this[int index]
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

			string M(string heading, ResourceStat stat)
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
