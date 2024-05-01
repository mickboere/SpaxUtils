using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can provide multiple <see cref="PoseInstruction"/>s for an <see cref="AnimatorPoser"/>.
	/// </summary>
	public interface IPoserInstructions
	{
		public PoseInstruction[] Instructions { get; }
	}
}
