using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Extensions for <see cref="RaycastHit"/>.
	/// </summary>
	public static class RaycastHitExtensions
	{
		public static bool TryGetComponentInParents<T>(this RaycastHit hit, out T result)
		{
			result = hit.transform.GetComponentInParent<T>();
			return result != null;
		}
	}
}
