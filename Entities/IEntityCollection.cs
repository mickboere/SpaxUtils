using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// A collection of <see cref="IEntity"/>s.
	/// Includes useful methods for retrieving entities and their components.
	/// </summary>
	/// <seealso cref="IEntity"/>
	/// <seealso cref="IEntityComponent"/>
	public interface IEntityCollection : IService
	{
		event Action<IEntity> AddedEntityEvent;
		event Action<IEntity> RemovedEntityEvent;

		/// <summary>
		/// Registers the given <see cref="IEntity"/>.
		/// </summary>
		void Add(IEntity entity);

		/// <summary>
		/// Deregisters the given <see cref="IEntity"/>.
		/// </summary>
		void Remove(IEntity entity);

		/// <summary>
		/// Returns entities implementing <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IEntity"/> implementation to look for.</typeparam>
		/// <param name="evaluation">Per-result <see cref="Func{T, bool}"/> evaluation.</param>
		/// <param name="exclude">The <see cref="IEntity"/>s to exclude from the results.</param>
		/// <returns>The resulting list of found entities.</returns>
		List<T> Get<T>(Func<T, bool> evaluation, params IEntity[] exclude) where T : class;

		/// <summary>
		/// Returns all entities implementing type <typeparamref name="T"/> except those that are <paramref name="exclude"/>d.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IEntity"/> implementation to look for.</typeparam>
		/// <param name="exclude"></param>
		/// <returns>The <see cref="IEntity"/>s to exclude from the results.</returns>
		List<T> Get<T>(params IEntity[] exclude) where T : class;

		/// <summary>
		/// Returns all <see cref="IEntityComponent"/>s implementing <typeparamref name="T"/> of all tracked <see cref="IEntity"/>s.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IEntityComponent"/> implementations to look for.</typeparam>
		/// <param name="entityEvaluation">Per-result evaluation of the current <see cref="IEntity"/>.</param>
		/// <param name="componentEvaluation">Per-result evaluation of the current <see cref="IEntityComponent"/>.</param>
		/// <param name="exclude">The <see cref="IEntity"/>s to exclude from the results.</param>
		/// <returns></returns>
		List<T> GetComponents<T>(Func<IEntity, bool> entityEvaluation, Func<T, bool> componentEvaluation, params IEntity[] exclude) where T : class, IEntityComponent;

		/// <summary>
		/// Returns all <see cref="IEntityComponent"/>s implementing <typeparamref name="T"/> of all tracked <see cref="IEntity"/>s.
		/// </summary>
		List<T> GetComponents<T>(params IEntity[] exclude) where T : class, IEntityComponent;
	}
}
