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
		public string Name => string.IsNullOrWhiteSpace(name) ? Identifier.LastDivision() : name;
		public float DefaultValue => defaultValue;
		public string Description => description;

		public bool CopyParent => copyParent;
		public string ParentIdentifier => parentIdentifier;
		public Color Color => copyParent ? default : color;
		public Sprite Icon => copyParent ? default : icon;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string identifier;
		[SerializeField] private string name;
		[SerializeField] private float defaultValue;
		[SerializeField, TextArea] private string description;
		[SerializeField, HideInInspector] private bool copyParent;
		[SerializeField, Conditional(nameof(copyParent), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string parentIdentifier;
		[SerializeField, Conditional(nameof(copyParent), true, false, true)] private Color color;
		[SerializeField, Conditional(nameof(copyParent), true, false, true)] private Sprite icon;

		public StatConfiguration(string identifier, string name, string description, float startingValue, Color color, Sprite icon)
		{
			this.identifier = identifier;
			this.name = name;
			this.description = description;
			this.defaultValue = startingValue;
			this.color = color;
			this.icon = icon;
		}
	}
}
