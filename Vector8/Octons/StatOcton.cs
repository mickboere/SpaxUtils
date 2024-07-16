using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that behaves as a <see cref="Vector8"/>, assigning an <see cref="EntityStat"/> to each member.
	/// </summary>
	[Serializable]
	public class StatOcton : IOcton
	{
		public Vector8 Vector8 => this;

		public EntityStat N { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string north;
		public EntityStat NE { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string northEast;
		public EntityStat E { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string east;
		public EntityStat SE { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string southEast;
		public EntityStat S { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string south;
		public EntityStat SW { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string southWest;
		public EntityStat W { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string west;
		public EntityStat NW { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string northWest;

		public StatOcton(EntityStat north, EntityStat northEast, EntityStat east, EntityStat southEast,
			EntityStat south, EntityStat southWest, EntityStat west, EntityStat northWest)
		{
			N = north;
			this.north = north.Identifier;
			NE = northEast;
			this.northEast = northEast.Identifier;
			E = east;
			this.east = east.Identifier;
			SE = southEast;
			this.southEast = southEast.Identifier;
			S = south;
			this.south = south.Identifier;
			SW = southWest;
			this.southWest = southWest.Identifier;
			W = west;
			this.west = west.Identifier;
			NW = northWest;
			this.northWest = northWest.Identifier;
		}

		public StatOcton(IEntity entity, string north, string northEast, string east, string southEast,
			string south, string southWest, string west, string northWest)
		{
			this.north = north;
			this.northEast = northEast;
			this.east = east;
			this.southEast = southEast;
			this.south = south;
			this.southWest = southWest;
			this.west = west;
			this.northWest = northWest;

			Initialize(entity);
		}

		public StatOcton(IEntity entity, StatOcton copy)
			: this(entity, copy.north, copy.northEast, copy.east, copy.southEast, copy.south, copy.southWest, copy.west, copy.northWest)
		{
		}

		/// <summary>
		/// Initialize an existing <see cref="StatOcton"/> by retrieving the defined stats from <paramref name="entity"/>.
		/// </summary>
		/// <param name="entity">The entity to initialize the octon with.</param>
		public void Initialize(IEntity entity)
		{
			N = entity.GetStat(north, true, 1f);
			NE = entity.GetStat(northEast, true, 1f);
			E = entity.GetStat(east, true, 1f);
			SE = entity.GetStat(southEast, true, 1f);
			S = entity.GetStat(south, true, 1f);
			SW = entity.GetStat(southWest, true, 1f);
			W = entity.GetStat(west, true, 1f);
			NW = entity.GetStat(northWest, true, 1f);
		}

		/// <summary>
		/// Implicit <see cref="Vector8"/> conversion.
		/// </summary>
		public static implicit operator Vector8(StatOcton octon)
		{
			return new Vector8(octon.N, octon.NE, octon.E, octon.SE, octon.S, octon.SW, octon.W, octon.NW);
		}

		public override string ToString()
		{
			return $"({M("N", N)}, {M("NE", NE)}, {M("E", E)}, {M("SE", SE)}, {M("S", S)}, {M("SW", SW)}, {M("W", W)}, {M("NW", NW)})";

			string M(string heading, EntityStat stat)
			{
				return $"\"{stat.Identifier}\"({heading})={stat.Value}";
			}
		}

		public string ToStringShort()
		{
			return $"({N.Value}, {NE.Value}, {E.Value}, {SE.Value}, {S.Value}, {SW.Value}, {W.Value}, {NW.Value})";
		}
	}
}
