using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class EntityApparel : MonoBehaviour, IEntityApparel
	{
		public List<string> Locations => coversLocation;

		[SerializeField, ConstDropdown(typeof(IBodyLocations))] private List<string> coversLocation;
	}

}
