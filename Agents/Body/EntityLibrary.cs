using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// ScriptableObject Service that holds a library of <see cref="Entity"/> prefabs,
	/// which can be instantiated with dependencies injected via the <see cref="Instantiate"/> method.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(EntityLibrary), menuName = "ScriptableObjects/" + nameof(EntityLibrary))]
	public class EntityLibrary : ScriptableObject, IService
	{
		public IReadOnlyDictionary<string, Entity> Entities
		{
			get
			{
				if (_entities == null)
				{
					_entities = new Dictionary<string, Entity>();
					for (int i = 0; i < entities.Count; i++)
					{
						_entities.Add(entities[i].ID, entities[i]);
					}
				}
				return _entities;
			}
		}
		private Dictionary<string, Entity> _entities;

		[SerializeField] private List<Entity> entities;

		/// <summary>
		/// Instantiates a new instance of the entity with the given ID, returning null if no such entity exists.
		/// The instance will be injected with the given dependencies, and will be activated if <paramref name="activate"/> is true.
		/// </summary>
		public Entity Instantiate(string id, string newId, Vector3 position = default, Quaternion rotation = default,
			IDependencyManager dependencies = null, RuntimeDataCollection runtimeData = null, bool activate = true)
		{
			if (!Entities.TryGetValue(id, out Entity entity))
			{
				SpaxDebug.Error($"Entity with ID '{id}' not found in EntityLibrary.");
				return null;
			}

			DependencyManager dependencyManager = new DependencyManager(dependencies, newId);
			dependencyManager.Bind(runtimeData);
			Entity instance = DependencyUtils.InstantiateDeactivated(entity.gameObject, position, rotation).GetComponent<Entity>();
			instance.Identification.ID = newId;
			DependencyUtils.Inject(instance.gameObject, dependencyManager, true, true);
			if (activate)
			{
				instance.gameObject.SetActive(true);
			}
			return instance;
		}
	}
}
