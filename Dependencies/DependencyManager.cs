using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;

namespace SpaxUtils
{
	/// <summary>
	/// Base <see cref="IDependencyManager"/> implementation.
	/// Able to bind, inject and locate dependencies.
	/// </summary>
	/// https://github.com/mickboere/SpaxUtils/blob/master/Dependencies/DependencyManager.cs
	public class DependencyManager : IDependencyManager
	{
		/// <summary>
		/// Prefix for created dependency <see cref="GameObject"/>s.
		/// </summary>
		public const string DEPENDENCY_OBJECT_PREFIX = "[Dependency] ";

		/// <summary>
		/// The index of this dependency manager, in order of creation with global being 0.
		/// Used in the <see cref="IdentifierPrefix"/>, which is used for debugging.
		/// </summary>
		public int GlobalIndex => GlobalDependencyManager.AllLocators.IndexOf(this);

		/// <summary>
		/// Prefix used in all the debug logs.
		/// </summary>
		protected string IdentifierPrefix => $"[{GlobalIndex}_{name}] ";

		/// <summary>
		/// The list of bindings belonging to this <see cref="IDependencyManager"/>.
		/// </summary>
		protected Dictionary<object, object> bindings;

		/// <summary>
		/// List of dependencies we're currently resolving, used to prevent circular dependencies.
		/// </summary>
		protected List<object> currentlyResolving;

		/// <summary>
		/// The parent <see cref="IDependencyManager"/>, used for finding dependencies we cannot find in our local <see cref="bindings"/>.
		/// </summary>
		protected IDependencyManager parent;

		/// <summary>
		/// The name of this dependency injector, used for debugging.
		/// </summary>
		protected string name;

		/// <summary>
		/// Object that is currently being injected, used for debugging.
		/// </summary>
		protected object context;

		/// <summary>
		/// Creates a new DependencyManager.
		/// </summary>
		/// <param name="parent">If a dependency can not be found using this locator, we will be able to search in the parent.</param>
		/// <param name="name">The name of this manager, used for debugging.</param>
		public DependencyManager(IDependencyManager parent = null, string name = "")
		{
			GlobalDependencyManager.AllLocators.Add(this);

			this.parent = parent;
			this.name = name;

			bindings = new Dictionary<object, object>();
			currentlyResolving = new List<object>();

			// Bind ourself unchecked since we want to overwrite our parent.
			BindUnchecked(typeof(IDependencyManager), this);
		}

		public virtual void Dispose()
		{
			bindings.Clear();
		}

		#region Public Methods

		/// <inheritdoc/>
		public virtual T Get<T>(bool includeParents = true, bool createIfNull = true)
		{
			return (T)Get(typeof(T), typeof(T), includeParents, createIfNull);
		}

		/// <inheritdoc/>
		public virtual T Get<T>(object key, bool includeParents = true, bool createIfNull = true)
		{
			return (T)Get(key, typeof(T), includeParents, createIfNull);
		}

		/// <inheritdoc/>
		public virtual object Get(object key, Type valueType, bool includeParents = true, bool createIfNull = true)
		{
			if (key == null || valueType == null)
			{
				throw new ArgumentException();
			}

			SpaxDebug.Log(IdentifierPrefix + "Get: ", $"Attempting to retrieve dependency using Key ({key}) of type ({valueType})", LogType.Notify, new Color(0.65f, 1f, 0.1f));

			// Try and find existing binding of type.
			if (TryGetBinding(key, valueType, includeParents, out object binding))
			{
				return binding;
			}
			// Try Get() on the parent if we have one.
			else if (includeParents && parent != null)
			{
				bool service = (typeof(IService).IsAssignableFrom(valueType));
				object parentDependency = parent.Get(key, valueType, includeParents, service);
				if (parentDependency != null)
				{
					return parentDependency;
				}
			}

			// Try to return new instance of requested type.
			if (createIfNull && TryInstantiateDependency(valueType, out object instance))
			{
				if (Bind(key, instance))
				{
					return instance;
				}
			}

			// Unable to find or create dependency.
			if (createIfNull)
			{
				SpaxDebug.Error(IdentifierPrefix + "The requested dependency could both not be found and not be created.", $"Regarding Key: '{key}' and Type: '{valueType}'.");
			}

			return null;
		}

		/// <inheritdoc/>
		public virtual object[] GetAll(Type type, bool includeParents = true)
		{
			List<object> dependencies = new List<object>();
			if (includeParents && parent != null)
			{
				dependencies.AddRange(parent.GetAll(type));
			}
			dependencies.AddRange(bindings.Values.Where((x) => type.IsAssignableFrom(x.GetType())));
			return dependencies.ToArray();
		}

		/// <inheritdoc/>
		public virtual T[] GetAll<T>(bool includeParents = true)
		{
			List<T> dependencies = new List<T>();
			if (includeParents && parent != null)
			{
				dependencies.AddRange(parent.GetAll<T>());
			}
			dependencies.AddRange(bindings.Values.OfType<T>());
			return dependencies.ToArray();
		}

		/// <inheritdoc/>
		public virtual bool TryGetBinding(object key, Type valueType, bool includeParent, out object value)
		{
			// Nullcheck
			if (key == null)
			{
				value = null;
				return false;
			}

			// Exact key match
			if (bindings.ContainsKey(key))
			{
				value = bindings[key];
				SpaxDebug.Notify(IdentifierPrefix + "TryGetBinding: ", $"Found <b>exact</b> binding: ({key}, {value})");
				return true;
			}

			// Assignable match
			foreach (KeyValuePair<object, object> binding in bindings)
			{
				if (valueType.IsAssignableFrom(binding.Value.GetType()))
				{
					value = binding.Value;
					SpaxDebug.Notify(IdentifierPrefix + "TryGetBinding: ", $"Found <b>assignable</b> binding: ({binding.Key}, {binding.Value})");
					return true;
				}
			}

			// We didn't find a reference within this locator, try the parent.
			if (includeParent && parent != null)
			{
				if (parent.TryGetBinding(key, valueType, includeParent, out value))
				{
					// The parent locator found the reference.
					return true;
				}
			}

			// The reference could not be found in both this and and the parent manager(s).
			SpaxDebug.Notify(IdentifierPrefix + "TryGetBinding: ", $"Could not find existing binding for key: ({key})");
			value = null;
			return false;
		}

		/// <inheritdoc/>
		public virtual bool Bind(object value)
		{
			return Bind(value.GetType(), value);
		}

		/// <inheritdoc/>
		public virtual bool Bind(object key, object value)
		{
			bool keyDupe = bindings.ContainsKey(key);
			if (!keyDupe)
			{
				BindUnchecked(key, value);
				return true;
			}

			bool providerDupe = false;
			object providerKey = "";
			if (value is IBindingKeyProvider bindingKeyProvider)
			{
				providerKey = bindingKeyProvider.BindingKey;
				providerDupe = bindings.ContainsKey(bindingKeyProvider.BindingKey);
				if (!providerDupe)
				{
					BindUnchecked(bindingKeyProvider.BindingKey, value);
					return true;
				}
			}

			if (keyDupe && providerDupe)
			{
				SpaxDebug.Error(IdentifierPrefix + "Could not bind dependency.",
					$"The existing bindings contain both a key/type dupe and provider dupe. Key: '{key}', Value: '{value}', Existing Value: '{bindings[key]}', ProviderKey: '{providerKey}'");
				return false;
			}

			SpaxDebug.Error(IdentifierPrefix + "Could not bind dependency.",
				$"The existing bindings contain a duplicate key. Key: '{key}', Value: '{value}', Existing Value: '{bindings[key]}', ProviderKey: '{providerKey}'");
			return false;
		}

		/// <inheritdoc/>
		public virtual void UnbindKey(object key)
		{
			bindings.Remove(key);
		}

		/// <inheritdoc/>
		public virtual void UnbindValue(object value)
		{
			List<KeyValuePair<object, object>> matches = bindings.Where(kvp => kvp.Value == value).ToList();
			foreach (var item in matches)
			{
				bindings.Remove(item.Key);
			}
		}

		/// <inheritdoc/>
		public virtual void Inject(object target, string dependencyMethod = IDependencyManager.INJECT_DEPENDENCIES_METHOD)
		{
			context = target;
			Type type = target.GetType();
			List<MethodInfo> methods = type.GetMethodsNamed(dependencyMethod);
			foreach (MethodInfo method in methods)
			{
				if (!TryResolveArguments(method, out object[] arguments))
				{
					SpaxDebug.Error(IdentifierPrefix + "Could not resolve arguments", $"for {type}");
					SpaxDebug.Log(GetDebugOutput());
					return;
				}

				method.Invoke(target, arguments);
			}
			context = null;
		}

		#endregion // Public Methods

		#region Private Methods

		/// <summary>
		/// Will try to create a new instance of <see cref="Type"/> <paramref name="type"/>, injecting all its dependencies.
		/// For regular classes the dependencies are injected in the constructor.
		/// For <see cref="Component"/>s the dependencies are injected in the <see cref="IDependencyManager.INJECT_DEPENDENCIES_METHOD"/> by default.
		/// </summary>
		/// <returns>Success in creating the instance.</returns>
		protected virtual bool TryInstantiateDependency(Type type, out object dependency)
		{
			// Components
			if (typeof(Component).IsAssignableFrom(type))
			{
				// IComponentService
				if (typeof(IServiceComponent).IsAssignableFrom(type))
				{
					GameObject dependencyObject = new GameObject(DEPENDENCY_OBJECT_PREFIX + type.Name);
					GameObject.DontDestroyOnLoad(dependencyObject);
					// Deactivate and later reactivate the gameobject so that we can inject the dependencies before Awake is called.
					dependencyObject.SetActive(false);
					dependency = dependencyObject.AddComponent(type);
					Inject(dependency);
					dependencyObject.SetActive(true);
					return true;
				}
				// Regular Component dependency - do not create: should be bound externally.
				else
				{
					dependency = null;
					return false;
				}
			}
			// ScriptableObject
			else if (typeof(ScriptableObject).IsAssignableFrom(type))
			{
				// Try to load the scriptable object from resources.
				dependency = Resources.Load(type.Name, type);
				return dependency != null;
			}
			// Regular dependency
			else
			{
				return TryCreateInstanceOfType(type, out dependency);
			}
		}

		protected virtual bool TryCreateInstanceOfType(Type type, out object instance)
		{
			instance = null;

			// Check if there is a Factory for this type, if so use that to create the instance.
			if (type.TryFindFactory(out Type factoryType))
			{
				IFactory factory = Activator.CreateInstance(factoryType) as IFactory;
				instance = factory.Create(this);
				return true;
			}

			// Abstract types and interfaces can only be resolved if they have a non-abstract implementation.
			if (type.IsAbstract || type.IsInterface)
			{
				List<Type> implementations = type.GetAllImplementations();
				if (implementations == null || implementations.Count == 0)
				{
					SpaxDebug.Error(IdentifierPrefix + "Requested type to instantiate is abstract and either has no assignable implementation or requires a Factory. ", $"Regarding (context:{context} < type:{type})");
					return false;
				}

				// Found implementations, try and instantiate any of them.
				foreach (Type implementation in implementations)
				{
					if (TryInstantiateDependency(implementation, out instance))
					{
						return true;
					}
				}

				SpaxDebug.Error("Unable to instantiate class");
				return false;
			}

			currentlyResolving.Add(type);

			// Resolve dependencies and create the instance.
			MethodBase[] constructors = type.GetConstructors();
			MethodBase constructor = constructors[0];

			if (constructors.Length > 1)
			{
				bool foundConstructor = false;
				foreach (MethodBase c in constructors)
				{
					if (c.GetParameters().Length == 0)
					{
						constructor = c;
						foundConstructor = true;
						break;
					}
				}

				if (!foundConstructor)
				{
					SpaxDebug.Error(IdentifierPrefix + "No safe constructor could be found for automated dependency. ", $"Regarding (context:{context} < type:{type})");
				}
			}

			// Try to resolve the constructor arguments.
			if (!TryResolveArguments(constructor, out object[] arguments))
			{
				SpaxDebug.Error(IdentifierPrefix + "Could not resolve arguments ", $"for {type}");
				instance = null;
				return false;
			}

			// Create the dependency instance and inject its dependencies in the constructor.
			instance = Activator.CreateInstance(type, arguments);

			currentlyResolving.Remove(type);

			SpaxDebug.Notify(IdentifierPrefix + "Created new dependency ", $"of type ({type}) for (context:{context})");
			return true;
		}

		/// <summary>
		/// Will attempt to resolve the parameters required for the given <see cref="MethodInfo"/> <paramref name="method"/>.
		/// </summary>
		protected virtual bool TryResolveArguments(MethodBase method, out object[] arguments)
		{
			ParameterInfo[] parameters = method.GetParameters();
			arguments = new object[parameters.Length];

			for (int i = 0; i < parameters.Length; i++)
			{
				// Use the parameter type as key.
				Type type = parameters[i].ParameterType;
				object key = type;

				// If there is a BindingIdentifierAttribute attached to the parameter, use that as our key instead.
				bool hasBindingIdentifier = false;
				if (DependencyUtils.TryGetBindingIdentifier(CustomAttributeData.GetCustomAttributes(parameters[i]), out string identifier))
				{
					hasBindingIdentifier = true;
					key = identifier;
				}

				// Prevent circular dependencies.
				if (currentlyResolving.Contains(key))
				{
					SpaxDebug.Error(IdentifierPrefix + "Circular Dependency detected!", $"Method={method.Name}, Dependency={key.GetType()}");
					return false;
				}

				bool optional = DependencyUtils.IsParameterOptional(parameters[i]);

				// If the parameter is an array, collect all binding values of the specified array type.
				if (!hasBindingIdentifier && type.IsArray)
				{
					Type elementType = type.GetElementType();
					object[] objectParams = GetAll(elementType);
					Array destinationArray = Array.CreateInstance(elementType, objectParams.Length);
					Array.Copy(objectParams, destinationArray, objectParams.Length);
					arguments[i] = destinationArray;
				}
				// Else if optional, try to get dependency but don't create if null.
				else if (optional)
				{
					arguments[i] = Get(key, type, true, false);
				}
				// Else try get or create.
				else
				{
					arguments[i] = Get(key, type, true, true);
				}

				// Dependency could not be found and not be created.
				if (arguments[i] == null && !optional)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Binds the given data without checking if the same key is already bound.
		/// </summary>
		protected virtual void BindUnchecked(object key, object value)
		{
			bindings[key] = value;
			SpaxDebug.Log(IdentifierPrefix + "Bind: ", $"({key}, {value})", LogType.Notify, new Color(0.4f, 1f, 0.9f));
		}

		#endregion // Private methods

		#region Debug

		public string GetDebugOutput()
		{
			StringBuilder sb = new StringBuilder();

			if (parent != null)
			{
				sb.AppendLine(parent.GetDebugOutput());
				sb.AppendLine();
			}

			sb.AppendLine($"{IdentifierPrefix} Output:");
			sb.AppendLine($"- ({bindings.Count}) Bindings:");

			foreach (KeyValuePair<object, object> binding in bindings)
			{
				sb.AppendLine($"\t({binding.Key}) , ({binding.Value})");
				//Debug.Log(binding);
			}

			return sb.ToString();
		}

		#endregion // Debug
	}
}
