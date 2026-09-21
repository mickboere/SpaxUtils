namespace SpaxUtils
{
	public static class ObjectExtensions
	{
		/// <summary>
		/// Whether <paramref name="obj"/> is non-null and, if it is a Unity object, not destroyed.
		/// </summary>
		public static bool Exists(this object obj)
		{
			return obj is UnityEngine.Object unityObject ? unityObject : obj != null;
		}
	}
}
