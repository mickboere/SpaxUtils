using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation for an <see cref="IEntity"/>, wraps around Unity's GameObject.
	/// </summary>
	[DefaultExecutionOrder(-1000)]
	public class Entity : MonoBehaviour, IEntity
	{
		public const string ID_NAME = "Name";
		public const string ID_POS = "Pos";
		public const string ID_ROT = "Rot";

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

		protected virtual string GameObjectNamePrefix => "[Entity]";
		protected virtual string GameObjectName =>
			string.IsNullOrEmpty(identification.Name) ? gameObject.name :
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

		protected virtual void LoadData(RuntimeDataCollection runtimeData)
		{
			// Load entity data.
			if (runtimeData != null)
			{
				// Data was supplied through dependencies.
				RuntimeData = runtimeData;
			}
			else if (runtimeDataService.CurrentProfile.TryGet(Identification.ID, out RuntimeDataCollection entityData))
			{
				// Saved data was found in data service.
				RuntimeData = entityData;
			}
			else
			{
				// Create new data.
				// We don't set the global data as the collection's parent because there's no guarantee this entity needs to be saved.
				RuntimeData = new RuntimeDataCollection(Identification.ID);
			}

			// Load or set entity name in data.
			if (RuntimeData.ContainsEntry(ID_NAME))
			{
				Identification.Name = RuntimeData.Get<string>(ID_NAME);
			}
			else
			{
				RuntimeData.Set(ID_NAME, Identification.Name);
			}
		}

		protected virtual void Awake()
		{
			// Check if our dependencies have been injected, if not, do so ourselves.
			// Thanks to DefaultExecutionOrderAttribute we should be able to inject all other components before they wake up.
			if (DependencyManager == null)
			{
				string dependencyManagerName = $"Entity:{Identification.Name}";
				SpaxDebug.Log("Entity did not have its dependencies injected.", $"Creating new DependencyManager using Global, named; '{dependencyManagerName}'.", LogType.Notify, Color.yellow, GameObject);
				DependencyUtils.Inject(GameObject, new DependencyManager(GlobalDependencyManager.Instance, dependencyManagerName), true, true);
			}
		}

		protected void Start()
		{
			gameObject.name = GameObjectName;

			if (RuntimeData.ContainsEntry(ID_POS) && Transform.position == Vector3.zero)
			{
				Transform.position = RuntimeData.Get<Vector3>(ID_POS);
				Transform.eulerAngles = RuntimeData.Get<Vector3>(ID_ROT);
			}
		}

		protected virtual void OnEnable()
		{
			Identification.IdentificationUpdatedEvent += OnIdentificationUpdatedEvent;
			entityCollection.Add(this);
		}

		protected virtual void OnDisable()
		{
			Identification.IdentificationUpdatedEvent -= OnIdentificationUpdatedEvent;
			entityCollection.Remove(this);
		}

		#region Data

		/// <inheritdoc/>
		public virtual void Save()
		{
			RuntimeData.Set(ID_POS, Transform.position);
			RuntimeData.Set(ID_ROT, Transform.eulerAngles);
			OnSaveEvent?.Invoke(RuntimeData);
			runtimeDataService.Save(RuntimeData);
		}

		/// <inheritdoc/>
		public virtual void SetDataValue(string identifier, object value)
		{
			RuntimeData.Set(identifier, value);
		}

		/// <inheritdoc/>
		public virtual object GetDataValue(string identifier)
		{
			return RuntimeData.Get(identifier);
		}

		/// <inheritdoc/>
		public virtual T GetDataValue<T>(string identifier)
		{
			return RuntimeData.Get<T>(identifier);
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
					EntityStat stat = new EntityStat(entry, null,
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
				IStatConfiguration setting = statLibrary.Get(identifier);
				RuntimeDataEntry data = new RuntimeDataEntry(identifier, setting == null ? defaultValueIfUndefined : setting.DefaultValue);
				RuntimeData.TryAdd(data);
				EntityStat stat = new EntityStat(data, null,
						setting != null ? setting.HasMinValue ? setting.MinValue : null : null,
						setting != null ? setting.HasMaxValue ? setting.MaxValue : null : null,
						setting != null ? setting.Decimals : DecimalMethod.Decimal);
				Stats.AddStat(identifier, stat);
				return stat;
			}

			return null;
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
			RuntimeData.Set(ID_NAME, identification.Name);
			gameObject.name = GameObjectName;
		}
	}
}
