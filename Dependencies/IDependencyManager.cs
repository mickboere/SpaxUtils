using System;
using UnityEngine;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for classes that are able to bind, inject, and locate dependencies.
	/// </summary>
	public interface IDependencyManager : IDisposable
	{
		/// <summary>
		/// The default method used for dependency injection.
		/// </summary>
		public const string INJECT_DEPENDENCIES_METHOD = "InjectDependencies";

		#region Locating

		/// <summary>
		/// Returns first dependency of type <typeparamref name="T"/>.
		/// </summary>
		T Get<T>(bool includeParents = true, bool createIfNull = true);

		/// <summary>
		/// Returns dependency of type <typeparamref name="T"/> bound with <see cref="object"/> <paramref name="key"/>.
		/// </summary>
		T Get<T>(object key, bool includeParents = true, bool createIfNull = true);

		/// <summary>
		/// Returns the dependency of <see cref="Type"/> <paramref name="valueType"/> bound with <see cref="object"/> <paramref name="key"/>.
		/// </summary>
		object Get(object key, Type valueType, bool includeParents = true, bool createIfNull = true);

		/// <summary>
		/// Returns all dependencies of type <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The type of entity to search for.</param>
		/// <returns>All dependencies of type <paramref name="type"/>.</returns>
		object[] GetAll(Type type, bool includeParents = true);

		/// <summary>
		/// Returns all dependencies of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type of entity to search for.</typeparam>
		/// <returns>All dependencies of type <typeparamref name="T"/>.</returns>
		T[] GetAll<T>(bool includeParents = true);

		/// <summary>
		/// Returns the binding registered with the given key.
		/// If the binding was done through type, use the <see cref="Type"/> as <paramref name="key"/>.
		/// </summary>
		bool TryGetBinding(object key, Type valueType, bool includeParent, out object value);

		#endregion

		#region Injecting

		/// <summary>
		/// Binds the given dependency <paramref name="value"/> with its <see cref="Type"/> as key.
		/// </summary>
		bool Bind(object value);

		/// <summary>
		/// Binds the given dependency <paramref name="value"/> with <paramref name="key"/>.
		/// </summary>
		bool Bind(object key, object value);

		/// <summary>
		/// Unbinds the dependency stored with <paramref name="key"/>.
		/// </summary>
		void UnbindKey(object key);

		/// <summary>
		/// Unbinds the dependency <paramref name="value"/>.
		/// </summary>
		void UnbindValue(object value);

		/// <summary>
		/// Inject dependencies on the <paramref name="target"/> object using the <paramref name="dependencyMethod"/> method.
		/// </summary>
		void Inject(object target, string dependencyMethod = INJECT_DEPENDENCIES_METHOD);

		#endregion

		string GetDebugOutput();
	}
}
