using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Struct containing a reference to an <see cref="IAct.Title"/> and a <see cref="ICombatMove"/>.
	/// </summary>
	[Serializable]
	public struct ActCombatPair
	{
		public string Act => act;
		public ICombatMove Move => move;

		[SerializeField, ConstDropdown(typeof(IActConstants))] private string act;
		[SerializeField, Expandable] private CombatMove move;
	}
}
