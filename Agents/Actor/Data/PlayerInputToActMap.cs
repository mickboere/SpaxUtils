using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// ScriptableObject containing input to act mappings.
	/// </summary>
	[CreateAssetMenu(fileName = "PlayerInputToActMap", menuName = "ScriptableObjects/PlayerInputToActMap")]
	public class PlayerInputToActMap : ScriptableObject
	{
		public IReadOnlyList<InputToActMapping> Mappings => mappings;

		[SerializeField] private List<InputToActMapping> mappings;
	}
}