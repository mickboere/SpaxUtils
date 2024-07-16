using System;
using System.Collections;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// AEMOI: Artificial Emotional Intelligence.
	/// </summary>
	public class AEMOI : IMind
	{
		public event Action<float> OnMindUpdateEvent;
		public event Action OnMindUpdatedEvent;

		public bool Active { get; private set; }

		public Vector8 Emotion => emotion;

		private Vector8 emotion;
		private Dictionary<IEntity, Vector8> stimuli = new Dictionary<IEntity, Vector8>();

		private AEMOISettings settings;
		private IOcton personality;

		public AEMOI(AEMOISettings settings, IOcton personality)
		{
			this.settings = settings;
			this.personality = personality;
		}

		/// <inheritdoc/>
		public void Activate(bool reset = false)
		{
			if (Active)
			{
				return; // Already active.
			}

			if (reset)
			{
				emotion = Vector8.One;
				stimuli.Clear();
			}

			Active = true;
		}

		/// <inheritdoc/>
		public void Deactivate()
		{
			if (!Active)
			{
				return; // Already inactive.
			}

			Active = false;
		}

		/// <inheritdoc/>
		public void Update(float delta)
		{
			// Gather senses.
			OnMindUpdateEvent?.Invoke(delta);

			// Simulate emotion fluids.
			emotion = emotion.Simulate(Vector8.One, personality.Vector8, settings.EmotionSimulation * delta);

			// Dampen emotions.
			emotion = emotion.Lerp(Vector8.One, settings.EmotionDamping * delta);

			// Dampen stimuli.
			foreach (KeyValuePair<IEntity, Vector8> kvp in stimuli)
			{
				stimuli[kvp.Key] = kvp.Value.Lerp(Vector8.Zero, settings.EmotionDamping * delta);
			}

			// Mind has been updated.
			OnMindUpdatedEvent?.Invoke();
		}

		/// <inheritdoc/>
		public void Stimulate(Vector8 stimulation, IEntity source = null)
		{
			if (source != null)
			{
				// An entity is responsible for this stimulation, add to stimuli.
				stimuli[source] += stimulation * personality.Vector8;
			}
			else
			{
				// No particular entity is responsible for this stimulation, add to emotion.
				emotion += stimulation * personality.Vector8;
			}
		}

		/// <inheritdoc/>
		public void Satisfy(Vector8 satisfaction, IEntity source = null)
		{
			Stimulate(-satisfaction, source);
		}

		/// <inheritdoc/>
		public Vector8 GetMotivation(out int index, out IEntity source)
		{
			Vector8 motivation = emotion;
			float highest = emotion.GetMax(out index);
			source = null;

			foreach (KeyValuePair<IEntity, Vector8> kvp in stimuli)
			{
				Vector8 stim = emotion + kvp.Value;
				float max = stim.GetMax(out int i);
				if (max > highest)
				{
					motivation = stim;
					highest = max;
					index = i;
					source = kvp.Key;
				}
			}

			return motivation;
		}
	}
}
