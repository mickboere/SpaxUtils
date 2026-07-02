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
		/// Per-entity behaviour-facing Motivation: the persistent Stimulation reservoir clamped under the
		/// Emotion envelope (cap = min(Emotion + BaseFloor, MAX_STIM)). This is what behaviours evaluate against.
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
		/// <see cref="Emotion"/> mapped onto a normalized [0,1] range via a concave curve (see AEMOISettings.EmotionNormalizationAnchor).
		/// Preferred over the raw [0,MAX_STIM] <see cref="Emotion"/> whenever emotion needs to sit alongside other normalized
		/// signals (e.g. Inclination/Personality in Balance): a just-actionable emotion already reads as a meaningful fraction.
		/// </summary>
		Vector8 EmotionNormalized { get; }

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
		/// Retrieves the current behaviour-facing Motivation (clamped) stored for <paramref name="source"/>.
		/// </summary>
		Vector8 RetrieveStimuli(IEntity source);

		/// <summary>
		/// Retrieves the raw situational Demand registered for <paramref name="source"/> this tick (debug/inspection).
		/// Zero if none. Persists through the tick; reset at the start of the next update.
		/// </summary>
		Vector8 RetrieveDemand(IEntity source);

		#region Stimulation

		/// <summary>
		/// CONTINUOUS input: sets (accumulates within the frame) the situational Demand toward <paramref name="source"/>.
		/// Stimulation tracks toward this Demand at the difficulty-scaled tracker rate; the buffer resets each tick.
		/// Senses (and secondary sense-behaviours) call this every frame with a per-axis level — NOT multiplied by delta.
		/// </summary>
		/// <param name="demand">The situational demand level (signed; foe-directed is negative).</param>
		/// <param name="source">The entity this demand concerns.</param>
		void SetDemand(Vector8 demand, IEntity source);

		/// <summary>
		/// IMPULSE input: adds directly into the persistent Stimulation reservoir for <paramref name="source"/>
		/// (hard-clamped to ±MAX_STIM). Use for discrete events (e.g. being hit). Bounded by the Emotion envelope
		/// in the behaviour-facing Motivation, so an impulse builds the response rather than bypassing the cap.
		/// </summary>
		/// <param name="stimulation">The impulse to apply.</param>
		/// <param name="source">The entity responsible for this stimulation.</param>
		void Stimulate(Vector8 stimulation, IEntity source);

		/// <summary>
		/// Satisfies the mind to calm its emotions and deform motivation.
		/// </summary>
		/// <param name="satisfaction">The satisfaction to apply.</param>
		/// <param name="source">The entity responsible for this satisfaction.</param>
		void Satisfy(Vector8 satisfaction, IEntity source);

		/// <summary>
		/// Immediately removes all stimuli and filters for <paramref name="source"/>,
		/// preventing it from influencing behaviour evaluation any further.
		/// Use when the source ceases to exist (e.g. on death) rather than <see cref="Satisfy"/>,
		/// which only decays values toward zero but leaves the entry in the stimuli dictionary.
		/// </summary>
		/// <param name="source">The entity whose stimuli should be cleared.</param>
		void ClearStimuli(IEntity source);

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
