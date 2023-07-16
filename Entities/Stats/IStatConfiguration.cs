using UnityEngine;

namespace SpaxUtils
{
	public interface IStatConfiguration
	{
		/// <summary>
		/// Constant string identifying this stat.
		/// </summary>
		string Identifier { get; }

		/// <summary>
		/// Default value for this stat if it is not set externally.
		/// </summary>
		float DefaultValue { get; }

		/// <summary>
		/// Defines whether this stat's value should be clamped to <see cref="MinValue"/>.
		/// </summary>
		bool HasMinValue { get; }

		/// <summary>
		/// The minimum value to clamp this stat to.
		/// </summary>
		float MinValue { get; }

		/// <summary>
		/// Defines whether this stat's value should be clamped to <see cref="MaxValue"/>.
		/// </summary>
		bool HasMaxValue { get; }

		/// <summary>
		/// The maximum value to clamp this stat to.
		/// </summary>
		float MaxValue { get; }

		/// <summary>
		/// User-facing name for this stat.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// User-facing description for this stat.
		/// </summary>
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
