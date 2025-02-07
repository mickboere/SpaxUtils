using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that behaves as a <see cref="SpaxUtils.Vector8"/>, assigning an <see cref="EntityStat"/> to each member.
	/// </summary>
	[Serializable]
	public class StatOctad : IOctad, IDisposable
	{
		public event Action<EntityStat> StatChangedEvent;

		public Vector8 Vector8 => this;

		public EntityStat N => stats[0];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] private string north;
		public EntityStat NE => stats[1];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] private string northEast;
		public EntityStat E => stats[2];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] private string east;
		public EntityStat SE => stats[3];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] private string southEast;
		public EntityStat S => stats[4];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] private string south;
		public EntityStat SW => stats[5];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] private string southWest;
		public EntityStat W => stats[6];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] private string west;
		public EntityStat NW => stats[7];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] private string northWest;

		private EntityStat[] stats;

		public StatOctad(EntityStat north, EntityStat northEast, EntityStat east, EntityStat southEast,
			EntityStat south, EntityStat southWest, EntityStat west, EntityStat northWest)
		{
			stats = new EntityStat[8];
			stats[0] = north;
			this.north = north.Identifier;
			stats[1] = northEast;
			this.northEast = northEast.Identifier;
			stats[2] = east;
			this.east = east.Identifier;
			stats[3] = southEast;
			this.southEast = southEast.Identifier;
			stats[4] = south;
			this.south = south.Identifier;
			stats[5] = southWest;
			this.southWest = southWest.Identifier;
			stats[6] = west;
			this.west = west.Identifier;
			stats[7] = northWest;
			this.northWest = northWest.Identifier;

			Subscribe();
		}

		public StatOctad(IEntity entity, string north, string northEast, string east, string southEast,
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

		public StatOctad(IEntity entity, StatOctad copy, Vector8 defaultValues)
			: this(entity, copy.north, copy.northEast, copy.east, copy.southEast, copy.south, copy.southWest, copy.west, copy.northWest, defaultValues)
		{
		}

		public EntityStat this[int index]
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

		/// <summary>
		/// Initialize an existing <see cref="StatOctad"/> by retrieving the defined stats from <paramref name="entity"/>.
		/// </summary>
		/// <param name="entity">The entity to initialize the octon with.</param>
		public void Initialize(IEntity entity)
		{
			Initialize(entity, Vector8.Zero);
		}

		/// <summary>
		/// Initialize an existing <see cref="StatOctad"/> by retrieving the defined stats from <paramref name="entity"/>.
		/// </summary>
		/// <param name="entity">The entity to initialize the octon with.</param>
		public void Initialize(IEntity entity, Vector8 defaultValues)
		{
			if (stats != null)
			{
				Unsubscribe();
			}
			stats = new EntityStat[8];
			stats[0] = entity.GetStat(north, true, defaultValues.N);
			stats[1] = entity.GetStat(northEast, true, defaultValues.NE);
			stats[2] = entity.GetStat(east, true, defaultValues.E);
			stats[3] = entity.GetStat(southEast, true, defaultValues.SE);
			stats[4] = entity.GetStat(south, true, defaultValues.S);
			stats[5] = entity.GetStat(southWest, true, defaultValues.SW);
			stats[6] = entity.GetStat(west, true, defaultValues.W);
			stats[7] = entity.GetStat(northWest, true, defaultValues.NW);
			Subscribe();
		}

		private void Subscribe()
		{
			foreach (EntityStat stat in stats)
			{
				stat.CompositeChangedEvent += OnStatChange;
			}
		}

		private void Unsubscribe()
		{
			foreach (EntityStat stat in stats)
			{
				stat.CompositeChangedEvent -= OnStatChange;
			}
		}

		public void Dispose()
		{
			Unsubscribe();
		}

		private void OnStatChange(CompositeFloatBase stat)
		{
			StatChangedEvent?.Invoke((EntityStat)stat);
		}

		/// <summary>
		/// Implicit <see cref="Vector8"/> conversion.
		/// </summary>
		public static implicit operator Vector8(StatOctad octad)
		{
			return new Vector8(octad.N, octad.NE, octad.E, octad.SE, octad.S, octad.SW, octad.W, octad.NW);
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
