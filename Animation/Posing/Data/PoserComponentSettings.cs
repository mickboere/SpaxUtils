using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "PoserSettings", menuName = "ScriptableObjects/Animation/Poser Settings")]
	public class PoserComponentSettings : ScriptableObject
	{
		public IReadOnlyList<PoserSettings> PosersSettings => posersSettings;

		[SerializeField] private List<PoserSettings> posersSettings;
	}
}
