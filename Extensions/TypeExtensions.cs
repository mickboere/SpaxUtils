using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace SpaxUtils
{
	public static class TypeExtensions
	{
		private static Dictionary<Type, List<Type>> assignableTypesCache = new Dictionary<Type, List<Type>>();

		public static List<T> GetAllPublicConstValues<T>(this Type type)
		{
			return type
				.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
				.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(T))
				.Select(x => (T)x.GetRawConstantValue())
				.ToList();
		}

		public static List<string> GetAllPublicConstStrings(this Type type, bool includeAdress, char separator = '/')
		{
			return type
				.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
				.Where(fieldInfo => fieldInfo.IsLiteral && !fieldInfo.IsInitOnly && fieldInfo.FieldType == typeof(string))
				.Select(fieldInfo => includeAdress ? $"{type.FullName.Replace('.', separator)}/{(string)fieldInfo.GetRawConstantValue()}" : (string)fieldInfo.GetRawConstantValue())
				.ToList();
		}

		/// <summary>
		/// Returns all types from all assemblies that are assignable to <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The type that needs to be assigned to.</param>
		/// <param name="predicate">Additional definable evaluation.</param>
		/// <returns>A list of types that are assignable to <paramref name="type"/> and pass the <paramref name="predicate"/>.</returns>
		public static List<Type> GetAllAssignableTypes(this Type type, Func<Type, bool> predicate = null)
		{
			if (!assignableTypesCache.ContainsKey(type))
			{
				assignableTypesCache[type] = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(s => s.GetTypes())
					.Where(p => type.IsAssignableFrom(p) && (predicate == null || predicate(p)))
					.ToList();
			}

			return new List<Type>(assignableTypesCache[type]);
		}

		/// <summary>
		/// Will attempt to find an <see cref="IFactory{T}"/> implementation that's able to produces inestances of type <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The desired product of the <see cref="IFactory{T}"/></param>
		/// <param name="factoryType">The found <see cref="Type"/> of <see cref="IFactory{T}"/> able to produce instances of type <paramref name="type"/>.</param>
		/// <returns>Whether finding the factory was a success.</returns>
		public static bool TryFindFactory(this Type type, out Type factoryType)
		{
			factoryType = AppDomain.CurrentDomain.GetAssemblies() // Get all assemblies.
				.SelectMany(s => s.GetTypes()) // Get all types from all assemblies.
				.Where(p => p.GetInterface(typeof(IFactory<>).Name) != null) // Get all factories.
				.Where(factory => factory.GetInterface(typeof(IFactory<>).Name).GetGenericArguments().Any(f => f.Equals(type))) // Get the factory that has implemented our type as generic argument in the interface.
				.FirstOrDefault();
			return factoryType != null;
		}

		/// <summary>
		/// Gets all of the methods from <paramref name="type"/> and sorts them by declaration depth.
		/// </summary>
		public static List<MethodInfo> GetMethodsSortedByDeclarationDepth(this Type type)
		{
			List<MethodInfo> methods = new List<MethodInfo>(type.GetMethods());
			methods.Sort((x, y) => y.DeclaringType.IsAssignableFrom(x.DeclaringType).CompareTo(x.DeclaringType.IsAssignableFrom(y.DeclaringType)));
			return methods;
		}

		/// <summary>
		/// Gets all of the methods named <paramref name="methodName"/> sorted by declaration depth.
		/// </summary>
		public static List<MethodInfo> GetMethodsNamed(this Type type, string methodName)
		{
			return type.GetMethodsSortedByDeclarationDepth().Where(m => m.Name == methodName).ToList();
		}

		/// <summary>
		/// Returns the first non-abstract implementation of type <paramref name="type"/>.
		/// </summary>
		public static Type GetImplementation(this Type type)
		{
			return type.GetAllAssignableTypes((t) => !t.IsAbstract && !t.IsInterface).FirstOrDefault();
		}

		/// <summary>
		/// Returns a new default instance of type <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of object to create.</param>
		/// <returns>The default instance.</returns>
		public static object GetDefault(this Type type)
		{
			if (type.IsValueType)
			{
				return Activator.CreateInstance(type);
			}
			return null;
		}
	}
}
