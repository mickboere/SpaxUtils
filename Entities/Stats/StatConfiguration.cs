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
		public string Name => name;
		public string Description => description;
		public float DefaultValue => defaultValue;
		public Color Color => color;
		public Sprite Icon => icon;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string identifier;
		[SerializeField] private string name;
		[SerializeField] private float defaultValue;
		[SerializeField] private Color color;
		[SerializeField] private Sprite icon;
		[SerializeField, TextArea] private string description;

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
