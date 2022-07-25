using System;

namespace SpaxUtils
{
	/// <summary>
	/// Static class containing generic extensions for enums with an underlying type of int.
	/// </summary>
	public static class EnumExtensions
	{
		public static T SetFlag<T>(this T e, T flag) where T : Enum
		{
			return (T)Enum.ToObject(typeof(T), (int)(object)e | (int)(object)flag);
		}

		public static T UnsetFlag<T>(this T e, T flag) where T : Enum
		{
			return (T)Enum.ToObject(typeof(T), (int)(object)e & (~(int)(object)flag));
		}

		public static T ToggleFlag<T>(this T e, T flag) where T : Enum
		{
			return (T)Enum.ToObject(typeof(T), (int)(object)e ^ (int)(object)flag);
		}
	}
}
