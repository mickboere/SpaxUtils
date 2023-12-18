using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public interface IStatConfiguration : IBaseStatConfiguration
	{
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

		/// <summary>
		/// Whether this stat has any sub-stats in <see cref="SubStats"/>.
		/// </summary>
		bool HasSubStats { get; }

		/// <summary>
		/// A list of sub-stat configurations which need to be configured together with the main stat.
		/// </summary>
		List<ISubStatConfiguration> SubStats { get; }
	}
}
