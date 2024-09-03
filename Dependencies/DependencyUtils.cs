using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SpaxUtils
{
	public static class DependencyUtils
	{
		#region Attribute Utils

		/// <summary>
		/// Will try to get a <see cref="BindingIdentifierAttribute"/> from an <see cref="IList{T}"/> of <see cref="CustomAttributeData"/> and returns its defined identifier.
		/// </summary>
		public static bool TryGetBindingIdentifier(IList<CustomAttributeData> attributes, out string identifier)
		{
			foreach (CustomAttributeData attribute in attributes)
			{
				if (TryGetBindingIdentifier(attribute, out identifier))
				{
					return true;
				}
			}

			identifier = null;
			return false;
		}

		/// <summary>
		/// Will try to get a <see cref="BindingIdentifierAttribute"/> from given <see cref="CustomAttributeData"/> and returns its defined identifier.
		/// </summary>
		public static bool TryGetBindingIdentifier(CustomAttributeData attribute, out string identifier)
		{
			if (attribute.AttributeType.Equals(typeof(BindingIdentifierAttribute)))
			{
				identifier = (string)attribute.ConstructorArguments.First((a) => a.ArgumentType.Equals(typeof(string))).Value;
				return true;
			}

			identifier = null;
			return false;
		}

		/// <summary>
		/// Will check if the parameter has an <see cref="OptionalAttribute"/> attached to it.
		/// </summary>
		/// <param name="parameter">The parameter to check the attributes for.</param>
		/// <returns>Whether the parameter has an <see cref="OptionalAttribute"/>.</returns>
		public static bool IsParameterOptional(ParameterInfo parameter)
		{
			return CustomAttributeData.GetCustomAttributes(parameter).Any(a => a.AttributeType.Equals(typeof(OptionalAttribute)));
		}

		#endregion // Attribute Utils

		/// <summary>
		/// Will get all <see cref="MonoBehaviour"/>s from <see cref="GameObject"/> <paramref name="root"/> and attempt to call InjectDependencies on them.
		/// Able to also <paramref name="includeChildren"/> and able to <paramref name="bindComponents"/> if they aren't bound already.
		/// </summary>
		/// <param name="root">The root <see cref="GameObject"/> to inject.</param>
		/// <param name="bindComponents">Defines whether any found components of type <see cref="IDependency"/> should be bound to the dependency injector.</param>
		public static void Inject(GameObject root, IDependencyManager dependencyManager, bool includeChildren = true, bool bindComponents = false)
		{
			List<MonoBehaviour> monoBehaviours = GatherMonoBehaviours(root, includeChildren);

			if (bindComponents)
			{
				// Bind IDependencyComponents.
				BindMonoBehaviours(monoBehaviours, dependencyManager);
			}

			// Inject
			foreach (MonoBehaviour monoBehaviour in monoBehaviours)
			{
				dependencyManager.Inject(monoBehaviour);
			}
		}

		/// <summary>
		/// Will bind any components implementing <see cref="IDependency"/> to the given <paramref name="dependencyManager"/>.
		/// </summary>
		public static void BindMonoBehaviours(GameObject root, IDependencyManager dependencyManager, bool includeChildren = true)
		{
			List<MonoBehaviour> monoBehaviours = GatherMonoBehaviours(root, includeChildren);

			// Bind IDependencyComponents.
			BindMonoBehaviours(monoBehaviours, dependencyManager);
		}

		/// <summary>
		/// Binds all components implementing <see cref="IDependency"/> to the <see cref="IDependencyManager"/>.
		/// </summary>
		public static void BindMonoBehaviours(List<MonoBehaviour> monoBehaviours, IDependencyManager dependencyManager)
		{
			foreach (MonoBehaviour component in monoBehaviours)
			{
				// Bind if component is IDependency.
				if (component is IDependency)
				{
					dependencyManager.Bind(component);
				}

				if (component is IDependencyProvider provider)
				{
					// Bind dependencies provided by IDependencyProvider.
					Dictionary<object, object> dependencies = provider.RetrieveDependencies();
					foreach (KeyValuePair<object, object> kvp in dependencies)
					{
						dependencyManager.Bind(kvp.Key, kvp.Value);
					}
				}
			}
		}

		public static List<MonoBehaviour> GatherMonoBehaviours(GameObject root, bool includeChildren = true)
		{
			// Retrieving components.
			List<MonoBehaviour> components = new List<MonoBehaviour>(includeChildren ? root.GetComponentsInChildren<MonoBehaviour>(true) : root.GetComponents<MonoBehaviour>());
			components.Sort(delegate (MonoBehaviour a, MonoBehaviour b)
			{
				DefaultExecutionOrder orderA = a.GetType().GetCustomAttribute<DefaultExecutionOrder>();
				DefaultExecutionOrder orderB = b.GetType().GetCustomAttribute<DefaultExecutionOrder>();
				return (orderA == null ? 0 : orderA.order).CompareTo(orderB == null ? 0 : orderB.order);
			});
			return components;
		}

		#region Instantiation

		/// <summary>
		/// (Safely) Instantiates an object deactivated, preventing its Awake function from being called until it is enabled in the future.
		/// </summary>
		public static GameObject InstantiateDeactivated(GameObject gameObject, Transform parent, Vector3 position, Quaternion rotation)
		{
			bool prefabWasActive = gameObject.activeSelf;
			GameObject instance;

			try
			{
				// Spawn the object deactivated so that we can inject dependencies before Awake is called.
				gameObject.SetActive(false);
				instance = GameObject.Instantiate(gameObject);

				// Parent and position.
				if (parent != null)
				{
					instance.transform.SetParent(parent);
				}
				instance.transform.localPosition = position;
				instance.transform.localRotation = rotation;
			}
			finally
			{
				// Returning prefab to original state.
				gameObject.SetActive(prefabWasActive);
			}

			return instance;
		}

		public static GameObject InstantiateDeactivated(GameObject gameObject)
		{
			return InstantiateDeactivated(gameObject, null, Vector3.zero, Quaternion.identity);
		}

		public static GameObject InstantiateDeactivated(GameObject gameObject, Transform parent)
		{
			return InstantiateDeactivated(gameObject, parent, Vector3.zero, Quaternion.identity);
		}

		public static GameObject InstantiateDeactivated(GameObject gameObject, Vector3 position, Quaternion rotation)
		{
			return InstantiateDeactivated(gameObject, null, position, rotation);
		}

		/// <summary>
		/// Instantiates the given <paramref name="gameObject"/> as a child of <paramref name="parent"/> on <paramref name="position"/>, <paramref name="rotation"/>,
		/// injecting all dependencies on all child <see cref="MonoBehaviour"/>s that implement <see cref="DependencyManager.INJECT_DEPENDENCIES_METHOD"/>.
		/// </summary>
		public static GameObject InstantiateAndInject(GameObject gameObject, Transform parent, Vector3 position, Quaternion rotation, IDependencyManager dependencies, bool includeChildren = true, bool bindComponents = true)
		{
			bool prefabWasActive = gameObject.activeSelf;

			// Create instance.
			GameObject instance = InstantiateDeactivated(gameObject, parent, position, rotation);

			// Dependency injection.
			Inject(instance, dependencies, includeChildren, bindComponents);

			// Activate.
			instance.SetActive(prefabWasActive);

			return instance;
		}

		public static GameObject InstantiateAndInject(GameObject gameObject, IDependencyManager dependencies, bool includeChildren = true, bool bindComponents = true)
		{
			return InstantiateAndInject(gameObject, null, Vector3.zero, Quaternion.identity, dependencies, includeChildren, bindComponents);
		}

		public static GameObject InstantiateAndInject(GameObject gameObject, Transform parent, IDependencyManager dependencies, bool includeChildren = true, bool bindComponents = true)
		{
			return InstantiateAndInject(gameObject, parent, Vector3.zero, Quaternion.identity, dependencies, includeChildren, bindComponents);
		}

		public static GameObject InstantiateAndInject(GameObject gameObject, Vector3 position, Quaternion rotation, IDependencyManager dependencies, bool includeChildren = true, bool bindComponents = true)
		{
			return InstantiateAndInject(gameObject, null, position, rotation, dependencies, includeChildren, bindComponents);
		}

		#endregion Instantiation
	}
}
