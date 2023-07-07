using UnityEngine;

namespace SpaxUtils
{
	public interface IStatConfiguration
	{
		string Identifier { get; }
		string Name { get; }
		float DefaultValue { get; }
		string Description { get; }

		/// <summary>
		/// Whether the <see cref="Color"/> and <see cref="Icon"/> should be copied from the parent stat.
		/// </summary>
		bool CopyParent { get; }

		/// <summary>
		/// The string identifying the parent stat of which to copy the <see cref="Color"/> and <see cref="Icon"/>.
		/// </summary>
		string ParentIdentifier { get; }

		Color Color { get; }
		Sprite Icon { get; }
	}
}
