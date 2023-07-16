using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IStatConfiguration"/> struct implementation used for linking parent stat data for easy access.
	/// </summary>
	public struct StatConfigStruct : IStatConfiguration
	{
		public string Identifier => config.Identifier;
		public float DefaultValue => config.DefaultValue;

		public bool HasMinValue => config.HasMinValue;

		public float MinValue => config.MinValue;

		public bool HasMaxValue => config.HasMaxValue;

		public float MaxValue => config.MaxValue;

		public string Name => config.Name;
		public string Description => config.Description;

		public bool CopyParent => config.CopyParent;
		public string ParentIdentifier => config.ParentIdentifier;

		public Color Color => CopyParent ? parent.Color : config.Color;
		public Sprite Icon => CopyParent ? parent.Icon : config.Icon;

		private IStatConfiguration config;
		private IStatConfiguration parent;

		public StatConfigStruct(IStatConfiguration config, IStatConfiguration parent)
		{
			this.config = config;
			this.parent = parent;
		}
	}
}
