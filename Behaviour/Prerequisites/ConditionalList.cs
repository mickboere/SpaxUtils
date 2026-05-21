using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Serializable wrapper around a <see cref="List{T}"/> of <see cref="IConditional"/> stored as managed references.
	/// Use <see cref="ConditionalListDrawer"/> for the custom inline editor.
	/// </summary>
	[Serializable]
	public class ConditionalList
	{
		[SerializeReference] public List<IConditional> items = new();

		public IReadOnlyList<IConditional> AsReadOnly() =>
			items != null ? (IReadOnlyList<IConditional>)items : Array.Empty<IConditional>();
	}
}
