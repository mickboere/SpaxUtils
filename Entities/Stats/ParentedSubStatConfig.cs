using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Constructs an <see cref="IStatConfiguration"/> for a <see cref="ISubStatConfiguration"/> using its parent config.
	/// </summary>
	public struct ParentedSubStatConfig : IStatConfiguration
	{
		public string Identifier => parentStatConfig.Identifier.SubStat(subStatConfig.Identifier);
		public float DefaultValue => subStatConfig.DefaultValue;

		public bool HasMinValue => subStatConfig.HasMinValue;

		public float MinValue => subStatConfig.MinValue;

		public bool HasMaxValue => subStatConfig.HasMaxValue;

		public float MaxValue => subStatConfig.MaxValue;
		public DecimalMethod Decimals => subStatConfig.Decimals;

		public string Name => parentStatConfig.Name + " " + subStatConfig.Name;
		public string Description => subStatConfig.Description;

		public bool CopyParent => true;
		public string ParentIdentifier => parentStatConfig.Identifier;

		public Color Color => parentStatConfig.Color;
		public Sprite Icon => parentStatConfig.Icon;

		public bool HasSubStats => false;
		public List<ISubStatConfiguration> SubStats => new List<ISubStatConfiguration>();

		private ISubStatConfiguration subStatConfig;
		private IStatConfiguration parentStatConfig;

		public ParentedSubStatConfig(ISubStatConfiguration subStatConfig, IStatConfiguration parentStatConfig)
		{
			this.subStatConfig = subStatConfig;
			this.parentStatConfig = parentStatConfig;
		}
	}
}
