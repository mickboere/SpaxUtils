using System;
using UnityEngine;
using SpaxUtils;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// A single stat setting.
	/// </summary>
	/// <seealso cref="StatLibrary"/>
	[Serializable]
	public class StatSetting : IStatSetting
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

		public StatSetting(string identifier, string name, string description, float startingValue, Color color, Sprite icon)
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
