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
	public interface IMind : IDisposable
	{
		/// <summary>
		/// Invoked when the mind is activated.
		/// </summary>
		event Action ActivatedEvent;

		/// <summary>
		/// Invoked when the mind is deactivated.
		/// </summary>
		event Action DeactivatedEvent;

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
		/// The mind's inclination profile responsible for stimulation weights.
		/// </summary>
		Vector8 Inclination { get; }

		/// <summary>
		/// The mind's personality profile, used by active behaviours to determine sub-behaviour.
		/// </summary>
		Vector8 Personality { get; }

		/// <summary>
		/// Collection of active stimuli being processed and the entities responsible for them.
		/// </summary>
		IReadOnlyDictionary<IEntity, Vector8> Stimuli { get; }

		/// <summary>
		/// The stimulation profile that is currently the strongest and the entity responsible for it.
		/// </summary>
		(Vector8 emotion, IEntity target) Motivation { get; }

		/// <summary>
		/// The <see cref="IMindBehaviour"/> currently in control of the Agent.
		/// </summary>
		IMindBehaviour ActiveBehaviour { get; }

		/// <summary>
		/// The entity chosen as the current target by the active behaviour's <see cref="IMindBehaviour.Evaluate"/> call.
		/// Authoritative targeting source; updated each tick before <see cref="MotivatedEvent"/>.
		/// </summary>
		IEntity ActiveTarget { get; }

		/// <summary>
		/// The agent's true internal emotional state — unsigned, slow-smoothed aggregate of |stim| across all tracked entities.
		/// Represents how the agent FEELS, not directed at any specific entity.
		/// </summary>
		Vector8 Emotion { get; }

		/// <summary>
		/// Behavioural lean as a Vector8 — per axis-pair lean combining base disposition (Inclination + Personality + Emotion)
		/// and directed emotion toward <see cref="ActiveTarget"/>, dampened by inertia.
		/// </summary>
		Vector8 Balance { get; }

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
		/// Stimulates the mind to spur its emotions and form a motivation.
		/// </summary>
		/// <param name="stimulation">The stimulation to apply.</param>
		/// <param name="source">The entity responsible for this stimulation.</param>
		void Stimulate(Vector8 stimulation, IEntity source);

		/// <summary>
		/// Satisfies the mind to calm its emotions and deform motivation.
		/// </summary>
		/// <param name="satisfaction">The satisfaction to apply.</param>
		/// <param name="source">The entity responsible for this satisfaction.</param>
		void Satisfy(Vector8 satisfaction, IEntity source);

		/// <summary>
		/// Adds a stimulation filter to stimuli from <paramref name="entity"/>.
		/// </summary>
		/// <param name="entity">The source of stimuli to filter.</param>
		/// <param name="filter">The stimulation multiplier.</param>
		void SetFilter(IEntity entity, Vector8 filter);

		/// <summary>
		/// Removes a stimulation filter to no longer filter <paramref name="entity"/>'s stimuli.
		/// </summary>
		/// <param name="entity">The source of stimuli to no longer filter.</param>
		void RemoveFilter(IEntity entity);

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
