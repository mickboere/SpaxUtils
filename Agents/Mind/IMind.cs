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
		event Action<float> OnMindUpdateEvent;

		/// <summary>
		/// Called once after the mind has been updated.
		/// </summary>
		event Action OnMindUpdatedEvent;

		/// <summary>
		/// Activates the mind to allow it to process emotions and act upon them.
		/// </summary>
		/// <param name="reset">Whether the mind's emotions should be reset before activating.</param>
		void Activate(bool reset);

		/// <summary>
		/// Deactivates the mind to prevent it from processing emotions and acting upon them.
		/// </summary>
		void Deactivate();

		/// <summary>
		/// Updates the mind to process its emotions.
		/// </summary>
		/// <param name="delta">The time in seconds between updates.</param>
		void Update(float delta);

		/// <summary>
		/// Stimulates the mind to spur its emotions.
		/// </summary>
		/// <param name="stimulation">The stimulation to apply.</param>
		/// <param name="source">The entity responsible for this stimulation, if any.</param>
		void Stimulate(Vector8 stimulation, IEntity source = null);

		/// <summary>
		/// Satisfies to mind to calm its emotions.
		/// </summary>
		/// <param name="satisfaction">The satisfaction to apply.</param>
		/// <param name="source">The entity responsible for this satisfaction, if any.</param>
		void Satisfy(Vector8 satisfaction, IEntity source = null);

		/// <summary>
		/// Return the current highest motivation and the entity responsible for it, if any.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="source"></param>
		/// <returns></returns>
		Vector8 GetMotivation(out int index, out IEntity source);
	}
}
