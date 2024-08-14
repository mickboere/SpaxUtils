using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// ScriptableObject containing input to act mappings.
	/// </summary>
	[CreateAssetMenu(fileName = "InputToActMap", menuName = "ScriptableObjects/InputToActMap")]
	public class InputToActMap : ScriptableObject
	{
		public IReadOnlyList<InputToActMapping> Mappings => mappings;

		[SerializeField] private List<InputToActMapping> mappings;
	}
}