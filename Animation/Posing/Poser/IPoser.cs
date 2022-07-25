using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can provide posing instructions for an <see cref="AnimatorPoser"/>.
	/// </summary>
	public interface IPoser
	{
		public PoseInstructions[] Instructions { get; }
	}
}
