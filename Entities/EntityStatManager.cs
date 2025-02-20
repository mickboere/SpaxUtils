using System.Collections.Generic;
using System;

#if UNITY_EDITOR
#endif

namespace SpaxUtils
{
	public class EntityStatManager
	{
		public StatCollection<EntityStat> Collection { get; private set; }

		private IEntity entity;
		private IStatLibrary statLibrary;
		private List<string> failedStats = new List<string>(); // Used to minimize error logs.

		public EntityStatManager(IEntity entity, IStatLibrary statLibrary)
		{
			this.entity = entity;
			this.statLibrary = statLibrary;
			Collection = new StatCollection<EntityStat>();
		}

		/// <inheritdoc/>
		public virtual EntityStat GetStat(string identifier, bool createDataIfNull = false, float defaultValueIfUndefined = 0f)
		{
			if (Collection.HasStat(identifier))
			{
				// Stat already exists.
				return Collection.GetStat(identifier);
			}
			else if (entity.RuntimeData.ContainsEntry(identifier))
			{
				// Data exists but stat does not, create the stat.
				RuntimeDataEntry entry = entity.RuntimeData.GetEntry(identifier);

				// Default floating point deserialization is double, convert to float.
				if (entry.Value is double)
				{
					entry.Value = Convert.ToSingle(entry.Value);
				}

				if (entry.Value is float)
				{
					IStatConfiguration setting = statLibrary.Get(identifier);
					EntityStat stat = new EntityStat(entity, entry, null,
						setting != null ? setting.HasMinValue ? setting.MinValue : null : null,
						setting != null ? setting.HasMaxValue ? setting.MaxValue : null : null,
						setting != null ? setting.Decimals : DecimalMethod.Decimal);

					Collection.AddStat(identifier, stat);
					return stat;
				}
				else if (!failedStats.Contains(identifier))
				{
					SpaxDebug.Error("Failed to create stat.", $"Data with ID '{identifier}' is of type '{entry.Value.GetType().FullName}'", entity.GameObject);
					failedStats.Add(identifier);
				}
			}
			else if (createDataIfNull)
			{
				// Data does not exist, create it along with the stat.
				statLibrary.TryGet(identifier, out IStatConfiguration setting);
				RuntimeDataEntry data = new RuntimeDataEntry(identifier, setting == null ? defaultValueIfUndefined : setting.DefaultValue);
				entity.RuntimeData.TryAdd(data);
				EntityStat stat = new EntityStat(entity, data, null,
						setting != null ? setting.HasMinValue ? setting.MinValue : null : null,
						setting != null ? setting.HasMaxValue ? setting.MaxValue : null : null,
						setting != null ? setting.Decimals : DecimalMethod.Decimal);
				Collection.AddStat(identifier, stat);
				return stat;
			}

			return null;
		}

		/// <inheritdoc/>
		public bool TryApplyStatCost(string stat, float cost, bool clamp, out float damage, out bool drained)
		{
			damage = 0f;
			drained = false;
			if (TryGetStat(stat, out EntityStat costStat))
			{
				// Damage unclamped, because performance's are active and will simply overdraw cost from "recoverable" (reservoir) stat.
				damage = costStat.Damage(cost, clamp, out bool d);
				drained = d || drained;
				return true;
			}
			return false;
		}

		/// <inheritdoc/>
		public bool TryApplyStatCost(string stat, float cost, bool clamp = false)
		{
			return TryApplyStatCost(stat, cost, clamp, out _, out _);
		}

		/// <inheritdoc/>
		public virtual bool TryGetStat(string identifier, out EntityStat stat)
		{
			stat = GetStat(identifier);
			return stat != null;
		}
	}
}
