using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that behaves as a <see cref="SpaxUtils.Vector8"/>, assigning an <see cref="EntityStat"/> to each member.
	/// </summary>
	[Serializable]
	public class StatOcton : IOcton
	{
		public Vector8 Vector8 => this;

		public EntityStat N { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string north;
		public EntityStat NE { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string northEast;
		public EntityStat E { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string east;
		public EntityStat SE { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string southEast;
		public EntityStat S { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string south;
		public EntityStat SW { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string southWest;
		public EntityStat W { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string west;
		public EntityStat NW { get; private set; }
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string northWest;

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
			string south, string southWest, string west, string northWest, Vector8 defaultValues)
		{
			this.north = north;
			this.northEast = northEast;
			this.east = east;
			this.southEast = southEast;
			this.south = south;
			this.southWest = southWest;
			this.west = west;
			this.northWest = northWest;

			Initialize(entity, defaultValues);
		}

		public StatOcton(IEntity entity, StatOcton copy, Vector8 defaultValues)
			: this(entity, copy.north, copy.northEast, copy.east, copy.southEast, copy.south, copy.southWest, copy.west, copy.northWest, defaultValues)
		{
		}

		/// <summary>
		/// Initialize an existing <see cref="StatOcton"/> by retrieving the defined stats from <paramref name="entity"/>.
		/// </summary>
		/// <param name="entity">The entity to initialize the octon with.</param>
		public void Initialize(IEntity entity)
		{
			Initialize(entity, Vector8.Zero);
		}

		/// <summary>
		/// Initialize an existing <see cref="StatOcton"/> by retrieving the defined stats from <paramref name="entity"/>.
		/// </summary>
		/// <param name="entity">The entity to initialize the octon with.</param>
		public void Initialize(IEntity entity, Vector8 defaultValues)
		{
			N = entity.GetStat(north, true, defaultValues.N);
			NE = entity.GetStat(northEast, true, defaultValues.NE);
			E = entity.GetStat(east, true, defaultValues.E);
			SE = entity.GetStat(southEast, true, defaultValues.SE);
			S = entity.GetStat(south, true, defaultValues.S);
			SW = entity.GetStat(southWest, true, defaultValues.SW);
			W = entity.GetStat(west, true, defaultValues.W);
			NW = entity.GetStat(northWest, true, defaultValues.NW);
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
