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

		public EntityStat[] Stats { get; private set; }

		public EntityStat N => Stats[0];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] public string north;
		public EntityStat NE => Stats[1];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] public string northEast;
		public EntityStat E => Stats[2];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] public string east;
		public EntityStat SE => Stats[3];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] public string southEast;
		public EntityStat S => Stats[4];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] public string south;
		public EntityStat SW => Stats[5];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] public string southWest;
		public EntityStat W => Stats[6];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] public string west;
		public EntityStat NW => Stats[7];
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), includeEmpty: true)] public string northWest;

		public StatOctad(EntityStat north, EntityStat northEast, EntityStat east, EntityStat southEast,
			EntityStat south, EntityStat southWest, EntityStat west, EntityStat northWest)
		{
			Stats = new EntityStat[8];
			Stats[0] = north;
			this.north = north.Identifier;
			Stats[1] = northEast;
			this.northEast = northEast.Identifier;
			Stats[2] = east;
			this.east = east.Identifier;
			Stats[3] = southEast;
			this.southEast = southEast.Identifier;
			Stats[4] = south;
			this.south = south.Identifier;
			Stats[5] = southWest;
			this.southWest = southWest.Identifier;
			Stats[6] = west;
			this.west = west.Identifier;
			Stats[7] = northWest;
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
			if (Stats != null)
			{
				Unsubscribe();
			}
			Stats = new EntityStat[8];
			Stats[0] = entity.Stats.GetStat(north, true, defaultValues.N);
			Stats[1] = entity.Stats.GetStat(northEast, true, defaultValues.NE);
			Stats[2] = entity.Stats.GetStat(east, true, defaultValues.E);
			Stats[3] = entity.Stats.GetStat(southEast, true, defaultValues.SE);
			Stats[4] = entity.Stats.GetStat(south, true, defaultValues.S);
			Stats[5] = entity.Stats.GetStat(southWest, true, defaultValues.SW);
			Stats[6] = entity.Stats.GetStat(west, true, defaultValues.W);
			Stats[7] = entity.Stats.GetStat(northWest, true, defaultValues.NW);
			Subscribe();
		}

		private void Subscribe()
		{
			foreach (EntityStat stat in Stats)
			{
				stat.CompositeChangedEvent += OnStatChange;
			}
		}

		private void Unsubscribe()
		{
			foreach (EntityStat stat in Stats)
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

		public string GetIdentifier(int index)
		{
			switch (index)
			{
				case 0: return north;
				case 1: return northEast;
				case 2: return east;
				case 3: return southEast;
				case 4: return south;
				case 5: return southWest;
				case 6: return west;
				case 7: return northWest;
				default:
					throw new ArgumentOutOfRangeException("index", $"Vector8 index needs to be between 0 and 7, but ({index}) was given!");
			}
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
