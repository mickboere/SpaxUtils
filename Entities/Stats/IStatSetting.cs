using UnityEngine;

namespace SpaxUtils
{
	public interface IStatSetting
	{
		string Identifier { get; }
		string Name { get; }
		string Description { get; }
		float DefaultValue { get; }
		Color Color { get; }
		Sprite Icon { get; }
	}
}
