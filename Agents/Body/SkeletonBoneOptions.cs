using UnityEngine;

namespace SpaxUtils
{
	public class SkeletonBoneOptions : MonoBehaviour, IExcludeFromSkeleton
	{
		public bool Exclude => exclude;
		public float Weight => weight;

		[SerializeField] private bool exclude;
		[SerializeField] private float weight = 1f; // TODO: Create custom Range slider that can exceed bounds like my MinMaxRange.
	}
}
