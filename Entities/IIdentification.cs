using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Object containing data used to identify an <see cref="IEntity"/>.
	/// Has a unique string <see cref="ID"/>, a <see cref="Name"/> and a collection of <see cref="Labels"/>.
	/// </summary>
	public interface IIdentification
	{
		/// <summary>
		/// Invoked when the identification labels have changed.
		/// </summary>
		event Action<IIdentification> LabelsChangedEvent;

		/// <summary>
		/// Unique ID used to identify this entity instance.
		/// </summary>
		string ID { get; }

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
