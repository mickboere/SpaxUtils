using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for an <see cref="IAgent"/>'s mind.
	/// Keeps track of the agent's emotions, objectives and memories.
	/// </summary>
	public interface IMind
	{
		/// <summary>
		/// Called once when the mind is updated, requesting all senses to call <see cref="Stimulate(Vector8, IEntity)"/>.
		/// The float value is the time delta between updates.
		/// </summary>
		event Action<float> UpdatingEvent;

		/// <summary>
		/// Called once while updating the mind after the agent's motivation has settled.
		/// </summary>
		event Action MotivatedEvent;

		/// <summary>
		/// Called once after the mind has been updated.
		/// </summary>
		event Action UpdatedEvent;

		/// <summary>
		/// Whether the mind is currently active and running.
		/// </summary>
		bool Active { get; }

		/// <summary>
		/// The mind's personality profile responsible for simulation weights.
		/// </summary>
		Vector8 Personality { get; }

		/// <summary>
		/// Collection of active stimuli being processed and the entities responsible for them.
		/// </summary>
		IReadOnlyDictionary<IEntity, Vector8> Stimuli { get; }

		/// <summary>
		/// The motivation profile that is currently the strongest and the entity responsible for it.
		/// </summary>
		(Vector8 motivation, IEntity target) Motivation { get; }

		/// <summary>
		/// Activates the mind to allow it to process stimuli and act upon them.
		/// </summary>
		/// <param name="reset">Whether the mind's emotions should be reset before activating.</param>
		void Activate(bool reset);

		/// <summary>
		/// Deactivates the mind to prevent it from processing stimuli and acting upon them.
		/// </summary>
		void Deactivate();

		/// <summary>
		/// Updates the mind to process its stimuli.
		/// </summary>
		/// <param name="delta">The time in seconds between updates.</param>
		void Update(float delta);

		/// <summary>
		/// Retrieves the current stimuli stored for <paramref name="source"/>.
		/// </summary>
		Vector8 RetrieveStimuli(IEntity source);

		#region Stimulation

		/// <summary>
		/// Stimulates the mind to spur its emotions.
		/// </summary>
		/// <param name="stimulation">The stimulation to apply.</param>
		/// <param name="source">The entity responsible for this stimulation.</param>
		void Stimulate(Vector8 stimulation, IEntity source);

		/// <summary>
		/// Satisfies to mind to calm its emotions.
		/// </summary>
		/// <param name="satisfaction">The satisfaction to apply.</param>
		/// <param name="source">The entity responsible for this satisfaction.</param>
		void Satisfy(Vector8 satisfaction, IEntity source);

		#endregion

		#region Behaviour

		/// <summary>
		/// Adds a single new executable behaviour.
		/// </summary>
		void AddBehaviour(IMindBehaviour behaviour);

		/// <summary>
		/// Adds a collection of new executable behaviours.
		/// </summary>
		void AddBehaviours(IEnumerable<IMindBehaviour> behaviours);

		/// <summary>
		/// Removes a single behaviour from the collection.
		/// </summary>
		void RemoveBehaviour(IMindBehaviour behaviour);

		/// <summary>
		/// Removes a collection of behaviours from the collection.
		/// </summary>
		void RemoveBehaviours(IEnumerable<IMindBehaviour> behaviours);

		#endregion
	}
}
