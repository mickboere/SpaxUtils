using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Extension methods for <see cref="GameObject"/>.
	/// </summary>
	public static class GameObjectExtensions
	{
		/// <summary>
		/// Will first try to find component in parent(s) then in children.
		/// </summary>
		public static T GetComponentRelative<T>(this GameObject gameObject)
		{
			T component = gameObject.GetComponentInParent<T>();
			if (component == null)
			{
				component = gameObject.GetComponentInChildren<T>();
			}
			return component;
		}

		/// <summary>
		/// Will first try to find component in parents then in children.
		/// </summary>
		public static bool TryGetComponentRelative<T>(this GameObject gameObject, out T component)
		{
			component = gameObject.GetComponentRelative<T>();
			return component != null;
		}
	}
}
