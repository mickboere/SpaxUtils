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

		#region Properties

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
		/// The modified runtime data.
		/// </summary>
		StatCollection<EntityStat> Stats { get; }

		/// <summary>
		/// Whether this entity is currently alive or not.
		/// </summary>
		bool Alive { get; }

		/// <summary>
		/// Age this entity has been alive for in seconds.
		/// </summary>
		float Age { get; }

		#endregion

		#region Data

		/// <summary>
		/// Save the entity's <see cref="RuntimeData"/>.
		/// </summary>
		void SaveData();

		/// <summary>
		/// Sets the value object of <see cref="RuntimeDataEntry"/> with ID <paramref name="identifier"/>.
		/// </summary>
		/// <param name="identifier">The ID of the <see cref="RuntimeDataEntry"/> of which to set the value.</param>
		void SetDataValue(string identifier, object value);

		/// <summary>
		/// Returns value of runtime data with ID <paramref name="identifier"/>.
		/// </summary>
		/// <param name="identifier">The identifier of the <see cref="RuntimeDataEntry"/> of which to return its value.</param>
		/// <returns>The value of the <see cref="RuntimeDataEntry"/> with ID <paramref name="identifier"/>.</returns>
		object GetDataValue(string identifier);

		/// <summary>
		/// Returns value of runtime data with ID <paramref name="identifier"/> as <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type to cast the value to.</typeparam>
		/// <param name="identifier">The identifier of the <see cref="RuntimeDataEntry"/> of which to return its value.</param>
		/// <returns>The value of the <see cref="RuntimeDataEntry"/> with ID <paramref name="identifier"/> as <typeparamref name="T"/>.</returns>
		T GetDataValue<T>(string identifier);

		/// <summary>
		/// Returns an <see cref="EntityStat"/> that wraps around the data with ID <paramref name="identifier"/>.
		/// <see cref="EntityStat"/> implements <see cref="CompositeFloatBase"/> which allows any amount of modifications to be done to the base data value without changing the data itself.
		/// </summary>
		/// <param name="identifier">The identifier of the runtime data for which to return a modifiable <see cref="EntityStat"/>.</param>
		/// <param name="createDataIfNull">If there is no existing data for <paramref name="identifier"/>, should it be created?</param>
		/// <param name="defaultValueIfUndefined">The default value for the newly created stat if there is no stat configuration data available.</param>
		/// <returns>An <see cref="EntityStat"/> that wraps around the data with ID <paramref name="identifier"/>.</returns>
		EntityStat GetStat(string identifier, bool createDataIfNull = false, float defaultValueIfUndefined = 0f);

		/// <summary>
		/// Checks whether an <see cref="EntityStat"/> wrapping around data with ID <paramref name="identifier"/> exists.
		/// <see cref="EntityStat"/> implements <see cref="CompositeFloatBase"/> which allows any amount of modifications to be done to the base data value without changing the data itself.
		/// </summary>
		/// <param name="identifier">The identifier of the runtime data for which to seek the modifiable <see cref="EntityStat"/>.</param>
		/// <param name="stat">The resulting <see cref="EntityStat"/>, if any.</param>
		/// <returns>Whether an <see cref="EntityStat"/> exists with ID <paramref name="identifier"/>.</returns>
		bool TryGetStat(string identifier, out EntityStat stat);

		/// <summary>
		/// Will try to retrieve stat from <paramref name="cost"/> and damage it by <paramref name="cost"/> * <paramref name="delta"/>.
		/// </summary>
		bool TryApplyStatCost(StatCost cost, float delta, out bool drained);

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
