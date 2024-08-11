using System;
using System.Collections;
using System.Collections.Generic;

namespace SpaxUtils
{
	public interface IRelations
	{
		/// <summary>
		/// Invoked when any of the relations have been altered.
		/// </summary>
		event Action RelationsUpdatedEvent;

		/// <summary>
		/// Invoked when a specific relation has been updated.
		/// </summary>
		event Action<string> RelationUpdatedEvent;

		/// <summary>
		/// An agent's memories define whether something's identity inpires fondness or hostility.
		/// - The Key can be either an agent's ID or any identification Label.
		/// - The Value is the quality of the memory; either positive or negative. Range is -1 to 1.
		/// </summary>
		IReadOnlyDictionary<string, float> Relations { get; }

		/// <summary>
		/// All relations invoking negative memories.
		/// </summary>
		IReadOnlyCollection<string> Enemies { get; }

		/// <summary>
		/// All relations invoking positive memories.
		/// </summary>
		IReadOnlyCollection<string> Friends { get; }

		/// <summary>
		/// Sets <paramref name="relation"/> to <paramref name="amount"/>.
		/// </summary>
		void Set(string relation, float amount);

		/// <summary>
		/// Adjust <paramref name="relation"/> by <paramref name="amount"/>.
		/// </summary>
		void Adjust(string relation, float amount);

		/// <summary>
		/// Scores the quality of a relationship for a single identification.
		/// </summary>
		/// <param name="id">The identification of which to score its identifier and labels.</param>
		/// <returns>The total relationship score </returns>
		float Score(IIdentification id);
	}
}
