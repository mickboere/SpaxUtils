using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Configuration for an entity stat.
	/// </summary>
	/// <seealso cref="StatLibraryService"/>
	[Serializable]
	public class StatConfiguration : IStatConfiguration
	{
		public string Identifier => identifier;
		public float DefaultValue => defaultValue;

		public bool HasMinValue { get; }
		public float MinValue { get; }
		public bool HasMaxValue { get; }
		public float MaxValue { get; }

		public string Name => string.IsNullOrWhiteSpace(name) ? Identifier.LastDivision() : name;
		public string Description => description;

		public bool CopyParent => copyParent;
		public string ParentIdentifier => parentIdentifier;
		public Color Color => copyParent ? default : color;
		public Sprite Icon => copyParent ? default : icon;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string identifier;
		[SerializeField] private float defaultValue;
		[SerializeField, HideInInspector] private bool hasMinValue;
		[SerializeField, Conditional(nameof(hasMinValue), drawToggle: true)] private float minValue;
		[SerializeField, HideInInspector] private bool hasMaxValue;
		[SerializeField, Conditional(nameof(hasMaxValue), drawToggle: true)] private float maxValue;
		[SerializeField] private string name;
		[SerializeField, TextArea] private string description;
		[SerializeField, HideInInspector] private bool copyParent;
		[SerializeField, Conditional(nameof(copyParent), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string parentIdentifier;
		[SerializeField, Conditional(nameof(copyParent), true, false, true)] private Color color;
		[SerializeField, Conditional(nameof(copyParent), true, false, true)] private Sprite icon;

		public StatConfiguration(string identifier, float defaultValue,
			bool hasMinValue, float minValue, bool hasMaxValue, float maxValue,
			string name, string description,
			bool copyParent, string parentIdentifier,
			Color color, Sprite icon)
		{
			this.identifier = identifier;
			this.defaultValue = defaultValue;
			this.hasMinValue = hasMinValue;
			this.minValue = minValue;
			this.hasMaxValue = hasMaxValue;
			this.maxValue = maxValue;
			this.name = name;
			this.description = description;
			this.copyParent = copyParent;
			this.parentIdentifier = parentIdentifier;
			this.color = color;
			this.icon = icon;
		}
	}
}
