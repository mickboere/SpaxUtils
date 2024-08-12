using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Data object which can be used to identify a unique entity or similar entities.
	/// </summary>
	/// <seealso cref="IEntity"/>
	public interface IIdentification
	{
		/// <summary>
		/// Invoked when the identification name or labels have changed.
		/// </summary>
		event Action<IIdentification> IdentificationUpdatedEvent;

		/// <summary>
		/// Unique ID used to identify this entity instance.
		/// </summary>
		string ID { get; set; }

		/// <summary>
		/// The name of this entity.
		/// </summary>
		string Name { get; set; }

		/// <summary>
		/// Labels or tags used to describe this entity.
		/// </summary>
		IList<string> Labels { get; }

		/// <summary>
		/// A reference to the entity this identification belongs to.
		/// </summary>
		IEntity Entity { get; }

		#region Label Methods

		/// <summary>
		/// Returns whether this identification matches any of the <paramref name="strings"/> given, checking for both the <see cref="ID"/> and <see cref="Labels"/>.
		/// </summary>
		bool Matches(IEnumerable<string> strings);

		/// <summary>
		/// Adds the provided <paramref name="labels"/> if they aren't added already.
		/// </summary>
		void Add(params string[] labels);

		/// <summary>
		/// Adds the provided <paramref name="labels"/> if they aren't added already.
		/// </summary>
		void Add(IEnumerable<string> labels);

		/// <summary>
		/// Removes the provided <paramref name="labels"/> if they are present.
		/// </summary>
		void Remove(params string[] labels);

		/// <summary>
		/// Removes the provided <paramref name="labels"/> if they are present.
		/// </summary>
		void Remove(IEnumerable<string> labels);

		/// <summary>
		/// Returns whether the entity has ALL of the provided <paramref name="labels"/>.
		/// </summary>
		bool HasAll(params string[] labels);

		/// <summary>
		/// Returns whether the entity has ALL of the provided <paramref name="labels"/>.
		/// </summary>
		bool HasAll(IEnumerable<string> labels);

		/// <summary>
		/// Returns whether the entity has ANY of the provided <paramref name="labels"/>.
		/// </summary>
		bool HasAny(params string[] labels);

		/// <summary>
		/// Returns whether the entity has ANY of the <paramref name="labels"/>.
		/// </summary>
		bool HasAny(IEnumerable<string> labels);

		#endregion
	}
}
