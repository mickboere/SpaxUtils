using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(-100)]
	public class GlobalDependencyManager : MonoBehaviour
	{
		/// <summary>
		/// Returns the global <see cref="IDependencyManager"/>.
		/// </summary>
		public static IDependencyManager Instance
		{
			get
			{
				if (WrapperInstance._instance == null)
				{
					WrapperInstance._instance = new DependencyManager(null, "Global");
				}
				return WrapperInstance._instance;
			}
		}
		private IDependencyManager _instance;

		/// <summary>
		/// A reference to the <see cref="GlobalDependencyManager"/> that holds a reference to the actual <see cref="IDependencyManager"/>.
		/// </summary>
		public static GlobalDependencyManager WrapperInstance
		{
			get
			{
				if (_wrapperInstance == null)
				{
					// Very edge case scenario that can only happen at the start of the application, once, if someone requests the
					// Dependencies before its Awake() has been called, so using Unity's Find function here ain't that horrible.
					GlobalDependencyManager existingDependencyManager = FindObjectOfType<GlobalDependencyManager>();
					if (existingDependencyManager != null)
					{
						_wrapperInstance = existingDependencyManager;
					}
					else
					{
						// Create new WrapperInstance (this).
						GameObject dependenciesObject = new GameObject(DependencyManager.DEPENDENCY_OBJECT_PREFIX + nameof(GlobalDependencyManager));
						dependenciesObject.SetActive(false);
						GlobalDependencyManager dependencies = dependenciesObject.AddComponent<GlobalDependencyManager>();
						_wrapperInstance = dependencies;
						dependenciesObject.SetActive(true);

						// Create new actual GlobalDependencies instance
						_wrapperInstance._instance = new DependencyManager(null, "Global");
					}
				}
				return _wrapperInstance;
			}
		}
		private static GlobalDependencyManager _wrapperInstance;

		/// <summary>
		/// A list containing all implementations of <see cref="IDependencyManager"/>
		/// </summary>
		public static List<IDependencyManager> AllLocators = new List<IDependencyManager>();

		[SerializeField] private bool debug;
		[SerializeField] private bool test;

		protected virtual void Awake()
		{
			if (_wrapperInstance != null && _wrapperInstance != this)
			{
				Destroy(this);
				return;
			}
			else
			{
				_wrapperInstance = this;
				DontDestroyOnLoad(this);
				SpaxDebug.Debugging = SpaxDebug.Debugging || debug;
			}

#if UNITY_EDITOR
			if (test)
			{
				gameObject.AddComponent<DependencyManagerTests>();
			}
#endif
		}
	}
}