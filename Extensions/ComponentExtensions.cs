using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Extension methods for <see cref="Component"/>.
	/// </summary>
	public static class ComponentExtensions
	{
		/// <summary>
		/// Get the path to this component.
		/// </summary>
		/// <param name="component">The component to get the path to.</param>
		/// <returns>The transform's path + the component name (parent>child>component)</returns>
		public static string GetPath(this Component component)
		{
			return component.transform.GetPath() + "/" + component.GetType().ToString();
		}
	}
}
