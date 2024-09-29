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
	[DefaultExecutionOrder(-9999), ExecuteInEditMode]
	public class Entity : MonoBehaviour, IEntity
	{
		/// <inheritdoc/>
		public event Action<RuntimeDataCollection> OnSaveEvent;

		/// <inheritdoc/>
		public GameObject GameObject => gameObject;

		/// <inheritdoc/>
		public Transform Transform => GameObject.transform;

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

		/// <inheritdoc/>
		public StatCollection<EntityStat> Stats { get; private set; }

		/// <inheritdoc/>
		public bool Alive { get; protected set; }

		/// <inheritdoc/>
		public float Age => (float)_age;
		private double _age;

		protected virtual string GameObjectNamePrefix => "[Entity]";
		protected virtual string GameObjectName =>
			string.IsNullOrWhiteSpace(identification.Name) ?
				string.IsNullOrWhiteSpace(identification.ID) ?
					Identification.Labels.Count == 0 ?
						gameObject.name :
					$"{GameObjectNamePrefix} {identification.TagLabels()}" :
				$"{GameObjectNamePrefix} {identification.ID}" :
			$"{GameObjectNamePrefix} {identification.Name}";

		[SerializeField] protected Identification identification;

		protected IEntityCollection entityCollection;
		protected IStatLibrary statLibrary;
		private RuntimeDataService runtimeDataService;
		private List<string> failedStats = new List<string>(); // Used to minimize error logs.

		public void InjectDependencies(
			IDependencyManager dependencyManager, IEntityComponent[] entityComponents, IEntityCollection entityCollection,
			RuntimeDataService runtimeDataService, IStatLibrary statLibrary,
			[Optional] RuntimeDataCollection runtimeData, [Optional] IIdentification identification)
		{
			if (DependencyManager != null)
			{
				SpaxDebug.Error("Entity already had its dependencies injected! ", $"You were probably too late with injecting and the Entity already took care of it itself. Next time, take a look at the DependencyUtils class.", GameObject);
				return;
			}

			DependencyManager = dependencyManager;
			Components = new List<IEntityComponent>(entityComponents);
			this.entityCollection = entityCollection;
			this.runtimeDataService = runtimeDataService;
			this.statLibrary = statLibrary;

			// Load identification.
			if (identification != null)
			{
				this.identification = new Identification(identification, this);
			}

			// Load runtime data.
			LoadData(runtimeData);

			// Initialize stats.
			Stats = new StatCollection<EntityStat>();
		}

		#region Unity Functions

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
			Identification.IdentificationUpdatedEvent += OnIdentificationUpdatedEvent;
			entityCollection.Add(this);
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
			entityCollection.Remove(this);
		}

		protected void Update()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying && PrefabStageUtility.GetCurrentPrefabStage() == null)
			{
				gameObject.name = GameObjectName;
			}
#endif

			if (Alive)
			{
				_age += Time.deltaTime;
			}
		}

		#endregion Unity Functions

		private void Initialize()
		{
			// Check if our dependencies have been injected, if not, do so ourselves.
			// Thanks to DefaultExecutionOrderAttribute we should be able to inject all other components before they wake up.
			if (DependencyManager == null)
			{
				string dependencyManagerName = $"Entity:{Identification.Name}";
				SpaxDebug.Log("Entity did not have its dependencies injected.", $"Creating new DependencyManager using Global, named; '{dependencyManagerName}'.", LogType.Notify, Color.yellow, GameObject);
				DependencyUtils.Inject(GameObject, new DependencyManager(GlobalDependencyManager.Instance, dependencyManagerName), true, true);

				gameObject.name = GameObjectName;
				Alive = true; // Active entity is being initialized so we must assume its alive.
				ApplyData();
			}
		}

		#region Data

		protected virtual void LoadData(RuntimeDataCollection baseData = null)
		{
			if (baseData != null)
			{
				// Data was supplied through dependencies.
				RuntimeData = baseData.Clone(Identification.ID);
			}
			else
			{
				// Create new data.
				// We don't set the global data as the collection's parent because there's no guarantee this entity needs to be saved.
				RuntimeData = new RuntimeDataCollection(Identification.ID);
			}

			if (runtimeDataService.CurrentProfile.TryGetEntry(Identification.ID, out RuntimeDataCollection entityData))
			{
				// Saved data was found in data service, append to existing data and overwrite duplicate data.
				RuntimeData.Append(entityData, true);
			}
		}

		protected virtual void ApplyData()
		{
			if (RuntimeData == null)
			{
				SpaxDebug.Error("No data to apply!", Identification.TagFull(), context: gameObject);
				return;
			}

			// Load or set entity name in data.
			if (RuntimeData.ContainsEntry(EntityDataIdentifiers.NAME))
			{
				Identification.Name = RuntimeData.GetValue<string>(EntityDataIdentifiers.NAME);
			}
			else
			{
				RuntimeData.SetValue(EntityDataIdentifiers.NAME, Identification.Name);
			}

			_age = RuntimeData.GetValue(EntityDataIdentifiers.AGE, 0d);
			if (Age > 1f)
			{
				Transform.position = RuntimeData.GetValue(EntityDataIdentifiers.POSITION, transform.position);
				Transform.eulerAngles = RuntimeData.GetValue(EntityDataIdentifiers.ROTATION, transform.eulerAngles);
			}
		}

		/// <inheritdoc/>
		public virtual void SaveData()
		{
			RuntimeData.SetValue(EntityDataIdentifiers.NAME, Identification.Name);
			RuntimeData.SetValue(EntityDataIdentifiers.ALIVE, Alive);
			RuntimeData.SetValue(EntityDataIdentifiers.AGE, _age);
			RuntimeData.SetValue(EntityDataIdentifiers.POSITION, Transform.position);
			RuntimeData.SetValue(EntityDataIdentifiers.ROTATION, Transform.eulerAngles);

			OnSaveEvent?.Invoke(RuntimeData);

			// TODO: Only save entity stats that do no match the default value in order to reduce filesize.
			runtimeDataService.SaveDataToProfile(RuntimeData);
		}

		/// <inheritdoc/>
		public virtual void SetDataValue(string identifier, object value)
		{
			RuntimeData.SetValue(identifier, value);
		}

		/// <inheritdoc/>
		public virtual object GetDataValue(string identifier)
		{
			return RuntimeData.GetValue(identifier);
		}

		/// <inheritdoc/>
		public virtual T GetDataValue<T>(string identifier)
		{
			return RuntimeData.GetValue<T>(identifier);
		}

		/// <inheritdoc/>
		public virtual EntityStat GetStat(string identifier, bool createDataIfNull = false, float defaultValueIfUndefined = 0f)
		{
			if (Stats.HasStat(identifier))
			{
				// Stat already exists.
				return Stats.GetStat(identifier);
			}
			else if (RuntimeData.ContainsEntry(identifier))
			{
				// Data exists but stat does not, create the stat.
				RuntimeDataEntry entry = RuntimeData.GetEntry(identifier);

				// Default floating point deserialization is double, convert to float.
				if (entry.Value is double)
				{
					entry.Value = Convert.ToSingle(entry.Value);
				}

				if (entry.Value is float)
				{
					IStatConfiguration setting = statLibrary.Get(identifier);
					EntityStat stat = new EntityStat(this, entry, null,
						setting != null ? setting.HasMinValue ? setting.MinValue : null : null,
						setting != null ? setting.HasMaxValue ? setting.MaxValue : null : null,
						setting != null ? setting.Decimals : DecimalMethod.Decimal);

					Stats.AddStat(identifier, stat);
					return stat;
				}
				else if (!failedStats.Contains(identifier))
				{
					SpaxDebug.Error("Failed to create stat.", $"Data with ID '{identifier}' is of type '{entry.Value.GetType().FullName}'", GameObject);
					failedStats.Add(identifier);
				}
			}
			else if (createDataIfNull)
			{
				// Data does not exist, create it along with the stat.
				statLibrary.TryGet(identifier, out IStatConfiguration setting);
				RuntimeDataEntry data = new RuntimeDataEntry(identifier, setting == null ? defaultValueIfUndefined : setting.DefaultValue);
				RuntimeData.TryAdd(data);
				EntityStat stat = new EntityStat(this, data, null,
						setting != null ? setting.HasMinValue ? setting.MinValue : null : null,
						setting != null ? setting.HasMaxValue ? setting.MaxValue : null : null,
						setting != null ? setting.Decimals : DecimalMethod.Decimal);
				Stats.AddStat(identifier, stat);
				return stat;
			}

			return null;
		}

		/// <inheritdoc/>
		public bool TryApplyStatCost(StatCost cost, float delta, out bool drained)
		{
			drained = false;
			if (TryGetStat(cost.Stat, out EntityStat costStat))
			{
				// Damage unclamped, because performance's are active and will simply overdraw cost from "recoverable" (reservoir) stat.
				costStat.Damage(cost.Cost * delta, false, out bool d);
				drained = d || drained;
				return true;
			}
			return false;
		}

		/// <inheritdoc/>
		public virtual bool TryGetStat(string identifier, out EntityStat stat)
		{
			stat = GetStat(identifier);
			return stat != null;
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

		private void OnIdentificationUpdatedEvent(IIdentification identification)
		{
			gameObject.name = GameObjectName;
		}
	}
}
