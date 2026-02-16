using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for a type of entity, our layer on top of Unity's <see cref="UnityEngine.GameObject"/> system.
	/// </summary>
	/// <seealso cref="IGameObject"/>
	/// <seealso cref="IDependency"/>
	public interface IEntity : IGameObject, IDependency, IIdentifiable
	{
		/// <summary>
		/// Invoked when the entity is about to be saved.
		/// Useful for any components that need to save their data to the collection before writing it away.
		/// </summary>
		event Action<RuntimeDataCollection> OnSaveEvent;

		/// <summary>
		/// Invoked when the entity is disabled or destroyed.
		/// </summary>
		event Action<IEntity> DeactivatedEvent;

		#region Properties

		/// <summary>
		/// Whether this entity is currently busy.
		/// </summary>
		bool Busy { get; }

		/// <summary>
		/// The entity's identification.
		/// </summary>
		IIdentification Identification { get; }

		/// <summary>
		/// The entity's main dependency manager.
		/// </summary>
		IDependencyManager DependencyManager { get; }

		/// <summary>
		/// The entity's components.
		/// </summary>
		IList<IEntityComponent> Components { get; }

		/// <summary>
		/// The entity's runtime data containing everything that will be saved between sessions.
		/// </summary>
		RuntimeDataCollection RuntimeData { get; }

		/// <summary>
		/// The <see cref="EntityStat"/>s that wrap around the <see cref="RuntimeData"/>.
		/// </summary>
		EntityStatManager Stats { get; }

		/// <summary>
		/// Whether the priority for this entity should be managed dynamically based on distance to nearest camera.
		/// </summary>
		bool DynamicPriority { get; set; }

		/// <summary>
		/// The priority level of this Entity, used for optimization.
		/// </summary>
		PriorityLevel Priority { get; set; }

		/// <summary>
		/// Whether this entity is running in debug mode.
		/// </summary>
		bool Debug { get; }

		#endregion

		#region Occupation

		/// <summary>
		/// Marks this entity as busy, being occupied with <paramref name="occupier"/>.
		/// </summary>
		void Occupy(object occupier);

		/// <summary>
		/// No longer marks this entity as being occupied with <paramref name="occupier"/>.
		/// </summary>
		void Deoccupy(object occupier);

		#endregion Occupation

		#region Optimization

		/// <summary>
		/// Add a subscriber to be invoked by the priority-optimized update callback.
		/// </summary>
		/// <param name="callback">The callback to be invoked.</param>
		void SubscribeOptimizedUpdate(Action<float> callback);

		/// <summary>
		/// Remove a subscriber to no longer be invoked by the priority-optimized update callback.
		/// </summary>
		/// <param name="callback">The callback to unsubscribe from the callback.</param>
		void UnsubscribeOptimizedUpdate(Action<float> callback);

		#endregion

		#region Data

		/// <summary>
		/// Manually save the entity's <see cref="RuntimeData"/>.
		/// </summary>
		void SaveData();

		#endregion

		#region Components

		/// <summary>
		/// Returns an <see cref="IEntityComponent"/> of Type <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of <see cref="IEntityComponent"/> to request.</param>
		/// <returns>An <see cref="IEntityComponent"/> of Type <paramref name="type"/>.</returns>
		IEntityComponent GetEntityComponent(Type type);

		/// <summary>
		/// Returns an <see cref="IEntityComponent"/> of Type <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of <see cref="IEntityComponent"/> to request.</param>
		/// <returns>Whether the getting of the <see cref="IEntityComponent"/> of Type <paramref name="type"/> succeeded.</returns>
		bool TryGetEntityComponent(Type type, out IEntityComponent entityComponent);

		/// <summary>
		/// Returns an <see cref="IEntityComponent"/> of Type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns>An <see cref="IEntityComponent"/> of Type <typeparamref name="T"/>.</returns>
		T GetEntityComponent<T>() where T : class, IEntityComponent;

		/// <summary>
		/// Returns an <see cref="IEntityComponent"/> of Type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns>Whether the getting of the <see cref="IEntityComponent"/> of Type <typeparamref name="T"/> succeeded.</returns>
		bool TryGetEntityComponent<T>(out T entityComponent) where T : class, IEntityComponent;

		#endregion
	}
}
