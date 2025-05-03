using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation for an <see cref="IEntity"/>, wraps around Unity's GameObject.
	/// </summary>
	[DefaultExecutionOrder(-10000), ExecuteInEditMode]
	public class Entity : MonoBehaviour, IEntity
	{
		/// <inheritdoc/>
		public event Action<RuntimeDataCollection> OnSaveEvent;

		#region Properties

		/// <inheritdoc/>
		public GameObject GameObject => gameObject;

		/// <inheritdoc/>
		public Transform Transform => GameObject.transform;

		/// <inheritdoc/>
		public string ID => Identification.ID;

		/// <inheritdoc/>
		public IIdentification Identification
		{
			get
			{
				if (Application.isPlaying && identification.Entity == null)
				{
					// Identity's entity property is null, recreate it with this entity.
					identification = new Identification(identification, this);
				}
				return identification;
			}
		}

		/// <inheritdoc/>
		public IDependencyManager DependencyManager { get; private set; }

		/// <inheritdoc/>
		public IList<IEntityComponent> Components { get; private set; }

		/// <inheritdoc/>
		public virtual RuntimeDataCollection RuntimeData { get; private set; }

		public EntityStatManager Stats { get; private set; }

		/// <inheritdoc/>
		public bool DynamicPriority
		{
			get { return dynamicPriority; }
			set
			{
				if (value != dynamicPriority && optimizedUpdateCallbacks.Count > 0)
				{
					if (value)
					{
						optimizationService.Subscribe(GameObject, entityOptimizationSettings.EntityOptimizationInterval, OnOptimizationPing);
					}
					else
					{
						optimizationService.Unsubscribe(GameObject);
					}
				}
				dynamicPriority = value;
			}
		}

		/// <inheritdoc/>
		public PriorityLevel Priority
		{
			get { return priority; }
			set
			{
				if (priority != value && optimizedUpdateCallbacks.Count > 0)
				{
					optimizationService.Switch(this, value);
				}
				priority = value;
			}
		}

		/// <inheritdoc/>
		public bool Debug { get { return debug; } set { debug = value; } }

		protected virtual string GameObjectNamePrefix => "[Entity]";
		protected virtual string GameObjectName =>
			identification != null ?
				string.IsNullOrWhiteSpace(identification.Name) ?
					string.IsNullOrWhiteSpace(identification.ID) ?
						Identification.Labels == null || Identification.Labels.Count == 0 ?
							gameObject.name :
						$"{GameObjectNamePrefix} {identification.TagLabels()}" :
					$"{GameObjectNamePrefix} {identification.ID}" :
				$"{GameObjectNamePrefix} {identification.Name}" :
			$"{GameObjectNamePrefix} UNKNOWN";

		#endregion Properties

		[SerializeField] protected Identification identification;
		[SerializeField] private bool autoSaveData;
		[Header("Optimization")]
		[SerializeField] private bool dynamicPriority;
		[SerializeField] private PriorityLevel priority;
		[SerializeField] private bool debug;

		protected IEntityCollection entityCollection;
		protected OptimizedCallbackService optimizationService;
		protected CameraService cameraService;
		protected EntityOptimizationSettings entityOptimizationSettings;
		protected RuntimeDataService runtimeDataService;
		protected SceneService sceneService;
		private bool initialized;
		private List<Action<float>> optimizedUpdateCallbacks = new List<Action<float>>();

		public void InjectDependencies(
			IDependencyManager dependencyManager, IEntityComponent[] entityComponents, IEntityCollection entityCollection,
			OptimizedCallbackService optimizationService, CameraService cameraService, EntityOptimizationSettings entityOptimizationSettings,
			RuntimeDataService runtimeDataService, IStatLibrary statLibrary, SceneService sceneService,
			[Optional] RuntimeDataCollection runtimeData, [Optional] IIdentification identification)
		{
			if (DependencyManager != null)
			{
				SpaxDebug.Error("Entity already had its dependencies injected! ", $"You were probably too late with injecting and the Entity already took care of it itself. For more information, take a look at the DependencyUtils class.", this);
				return;
			}

			DependencyManager = dependencyManager;
			Components = new List<IEntityComponent>(entityComponents);
			this.entityCollection = entityCollection;
			this.optimizationService = optimizationService;
			this.cameraService = cameraService;
			this.entityOptimizationSettings = entityOptimizationSettings;
			this.runtimeDataService = runtimeDataService;
			this.sceneService = sceneService;

			// Load identification.
			if (identification != null)
			{
				this.identification = new Identification(identification, this);
			}

			// Load runtime data.
			LoadData(runtimeData);

			// Initialize stats.
			Stats = new EntityStatManager(this, statLibrary);
		}

		#region Internal

		protected virtual void Awake()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif

			Initialize();
		}

		protected void Start()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif
		}

		protected virtual void OnEnable()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif

			Initialize();

			if (gameObject.activeInHierarchy)
			{
				// Entity could have been disabled during initialization, ensure its not.
				Identification.IdentificationUpdatedEvent += OnIdentificationUpdatedEvent;
				entityCollection.Add(this);
			}
		}

		protected virtual void OnDisable()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif

			Identification.IdentificationUpdatedEvent -= OnIdentificationUpdatedEvent;
			entityCollection?.Remove(this);
		}

		protected virtual void OnDestroy()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif

			Identification.IdentificationUpdatedEvent -= OnIdentificationUpdatedEvent;
			entityCollection?.Remove(this);
		}

		protected virtual void Update()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying && PrefabStageUtility.GetCurrentPrefabStage() == null)
			{
				gameObject.name = GameObjectName;
			}
#endif
		}

		protected virtual void OnValidate()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying && !gameObject.scene.name.IsNullOrEmpty() && PrefabStageUtility.GetCurrentPrefabStage() == null &&
				identification != null && identification.ID.IsNullOrEmpty())
			{
				identification.ID = Guid.NewGuid().ToString();
			}
#endif
		}

		#endregion Internal

		private void Initialize()
		{
			// Check if our dependencies have been injected, if not, do so ourselves.
			// Thanks to DefaultExecutionOrderAttribute we should be able to inject all other components before they wake up.
			if (DependencyManager == null)
			{
				DependencyUtils.Inject(GameObject, new DependencyManager(GlobalDependencyManager.Instance, $"Entity:{Identification.Name}"), true, true);
			}

			if (!initialized)
			{
				initialized = true;
				gameObject.name = GameObjectName;
				ApplyData();
				if (autoSaveData)
				{
					runtimeDataService.SavingCurrentToDiskEvent += OnSavingEvent;
				}
			}
		}

		#region Optimization

		/// <inheritdoc/>
		public void SubscribeOptimizedUpdate(Action<float> callback)
		{
			if (optimizedUpdateCallbacks.Count == 0)
			{
				// Begin listening to optimized update callbacks.
				optimizationService.Subscribe(this, priority, OnOptimizedUpdate);
				if (dynamicPriority)
				{
					// Priority is dynamic, ping every interval to update.
					optimizationService.Subscribe(GameObject, entityOptimizationSettings.EntityOptimizationInterval, OnOptimizationPing);
				}
			}
			optimizedUpdateCallbacks.Add(callback);
		}

		/// <inheritdoc/>
		public void UnsubscribeOptimizedUpdate(Action<float> callback)
		{
			optimizedUpdateCallbacks.Remove(callback);
			if (optimizedUpdateCallbacks.Count == 0)
			{
				// Entity no longer needs to be listening to optimized updates.
				optimizationService.Unsubscribe(this);
				if (dynamicPriority)
				{
					optimizationService.Unsubscribe(GameObject);
				}
			}
		}

		private void OnOptimizedUpdate(float delta)
		{
			// Invoke all subscriptions.
			List<Action<float>> callbacks = new List<Action<float>>(optimizedUpdateCallbacks);
			foreach (Action<float> callback in callbacks)
			{
				callback(delta);
			}
		}

		private void OnOptimizationPing(float delta)
		{
			// Automatically change priority depending on distance to nearest camera.
			Priority = entityOptimizationSettings.GetPriorityBySqrDistance(cameraService.GetSqrDistanceToMainCamera(Transform.position));
		}

		#endregion Optimization

		#region Data

		protected virtual void LoadData(RuntimeDataCollection baseData = null)
		{
			if (baseData != null)
			{
				// Data was supplied through dependencies.
				//RuntimeData = baseData.Clone(Identification.ID);
				RuntimeData = baseData;
			}
			else
			{
				// Create new data.
				// We don't set the global data as the collection's parent because there's no guarantee this entity needs to be saved.
				RuntimeData = new RuntimeDataCollection(Identification.ID);
			}

			if (runtimeDataService.EnsureCurrentProfile().TryGetEntry(Identification.ID, out RuntimeDataCollection loadedData))
			{
				// Saved data was found in data service, append to existing data and overwrite duplicate data.
				RuntimeData.Append(loadedData, true);
			}
		}

		protected virtual void ApplyData()
		{
			if (RuntimeData == null)
			{
				SpaxDebug.Error("No data to apply!", Identification.TagFull(), context: gameObject);
				return;
			}

			// Load entity name from data.
			if (RuntimeData.ContainsEntry(EntityDataIdentifiers.NAME))
			{
				Identification.Name = RuntimeData.GetValue<string>(EntityDataIdentifiers.NAME);
			}

			// Retrieve pre-set priority level to override dynamic priority, if any.
			if (RuntimeData.TryGetValue(EntityDataIdentifiers.PRIORITY, out int prio))
			{
				DynamicPriority = false;
				Priority = (PriorityLevel)prio;
			}

			// Retrieve whether this entity should be run in debug mode.
			if (RuntimeData.TryGetValue(EntityDataIdentifiers.DEBUG, out bool debug))
			{
				Debug = debug;
			}

			// If the entity has been turned off, disable the game object.
			if (RuntimeData.GetValue(EntityDataIdentifiers.OFF, false))
			{
				gameObject.SetActive(false);
			}
		}

		/// <inheritdoc/>
		public virtual void SaveData()
		{
			OnSavingData();
			OnSaveEvent?.Invoke(RuntimeData);
			runtimeDataService.SaveDataToProfile(RuntimeData);
		}

		protected virtual void OnSavingEvent(RuntimeDataCollection _)
		{
			SaveData();
		}

		#endregion

		#region Entity Components

		/// <inheritdoc/>
		public virtual IEntityComponent GetEntityComponent(Type type)
		{
			// Type must implement IEntityComponent, else it can't possibly be in our list.
			if (!typeof(IEntityComponent).IsAssignableFrom(type))
			{
				SpaxDebug.Error("GetEntityComponent ", $"Type {type} is not assignable to IEntityComponent.", this);
				return null;
			}
			return Components.FirstOrDefault((e) => type.IsAssignableFrom(e.GetType()));
		}

		/// <inheritdoc/>
		public virtual bool TryGetEntityComponent(Type type, out IEntityComponent entityComponent)
		{
			entityComponent = GetEntityComponent(type);
			return entityComponent != null;
		}

		/// <inheritdoc/>
		public virtual T GetEntityComponent<T>() where T : class, IEntityComponent
		{
			IEntityComponent entityComponent = GetEntityComponent(typeof(T));
			return entityComponent == null ? null : (T)entityComponent;
		}

		/// <inheritdoc/>
		public virtual bool TryGetEntityComponent<T>(out T entityComponent) where T : class, IEntityComponent
		{
			entityComponent = GetEntityComponent<T>();
			return entityComponent != null;
		}

		#endregion Entity Component Methods

		protected virtual void OnSavingData()
		{
			//RuntimeData.SetValue(EntityDataIdentifiers.NAME, Identification.Name);
		}

		private void OnIdentificationUpdatedEvent(IIdentification identification)
		{
			gameObject.name = GameObjectName;
		}
	}
}
