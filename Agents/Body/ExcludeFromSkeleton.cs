using UnityEngine;

namespace SpaxUtils
{
	/// <summary>Keeps this transform and everything under it out of the skeleton walk.</summary>
	public class ExcludeFromSkeleton : MonoBehaviour, IExcludeFromSkeleton { public bool Exclude => true; }
}
