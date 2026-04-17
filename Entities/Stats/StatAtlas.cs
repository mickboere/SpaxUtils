using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Asset that contains a collection of <see cref="StatMap"/>s.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(StatAtlas), menuName = "ScriptableObjects/Stats/" + nameof(StatAtlas))]
	public class StatAtlas : ScriptableObject
	{
		public IReadOnlyList<StatMap> Maps => maps;

		[SerializeField] private List<StatMap> maps;
	}
}
