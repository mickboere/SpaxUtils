using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation for an <see cref="IEntity"/>, wraps around Unity's GameObject.
	/// </summary>
	[DefaultExecutionOrder(-100)]
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
			GameObjectNamePrefix +
			(string.IsNullOrEmpty(identification.Name) ? " " : $" {identification.Name} ");
		// + (identification.Labels != null && identification.Labels.Count > 0 ? $"({string.Join(", ", identification.Labels)})" : "");

		[SerializeField] protected Identification identification;

		protected IEntityCollection entityCollection;
		protected StatLibrary statLibrary;
		private RuntimeDataService runtimeDataService;

		public void InjectDependencies(
			IDependencyManager dependencyManager, IEntityComponent[] entityComponents, IEntityCollection entityCollection,
			RuntimeDataService runtimeDataService, StatLibrary statLibrary,
			[Optional] RuntimeDataCollection runtimeData)
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

			// Load entity data.
			if (runtimeData != null)
			{
				RuntimeData = runtimeData;
			}
			else if (runtimeDataService.CurrentProfile.TryGet(Identification.ID, out RuntimeDataCollection entityData))
			{
				RuntimeData = entityData;
			}
			else
			{
				RuntimeData = new RuntimeDataCollection(Identification.ID);
			}

			Stats = new StatCollection<EntityStat>();
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
			if (RuntimeData.ContainsEntry(ID_NAME))
			{
				Identification.Name = RuntimeData.Get<string>(ID_NAME);
			}
			else
			{
				RuntimeData.Set(ID_NAME, Identification.Name);
			}

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
		public void Save()
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

		private List<string> failedStats = new List<string>(); // Used to minimize error logs.

		/// <inheritdoc/>
		public virtual EntityStat GetStat(string identifier, bool createDataIfNull = false)
		{
			if (Stats.HasStat(identifier))
			{
				// Stat already exists.
				return Stats.GetStat(identifier);
			}
			else if (RuntimeData.ContainsEntry(identifier))
			{
				// Data exists but stat does not, create the stat.
				RuntimeDataEntry data = RuntimeData.GetEntry(identifier);
				if (data.Value is float)
				{
					EntityStat stat = new EntityStat(data);
					Stats.AddStat(identifier, stat);
					return stat;
				}
				else if (!failedStats.Contains(identifier))
				{
					SpaxDebug.Error("Failed to create stat.", $"Data with ID '{identifier}' is not a float value.", GameObject);
					failedStats.Add(identifier);
				}
			}
			else if (createDataIfNull)
			{
				// Data does not exist, create it along with the stat.
				IStatSetting setting = statLibrary.Get(identifier);
				if (setting != null)
				{
					RuntimeDataEntry data = new RuntimeDataEntry(identifier, setting.DefaultValue);
					RuntimeData.TryAdd(data);
					EntityStat stat = new EntityStat(data);
					Stats.AddStat(setting.Identifier, stat);
					return stat;
				}
				else if (!failedStats.Contains(identifier))
				{
					SpaxDebug.Error("Failed to create data.", $"Stat settings could not be retrieved for: {identifier}", GameObject);
					failedStats.Add(identifier);
				}
			}

			return null;
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
