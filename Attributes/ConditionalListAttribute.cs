using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Marks a <see cref="System.Collections.Generic.List{T}"/> of <see cref="IConditional"/> with
	/// <c>[SerializeReference]</c> for inline polymorphic editing without external asset files.
	/// </summary>
	public class ConditionalListAttribute : PropertyAttribute { }
}
