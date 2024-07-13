using System.Collections;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// AEMOI: Artificial Emotional Intelligence.
	/// </summary>
	public class AEMOI : IMind
	{
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

		public void Deactivate()
		{
			if (!Active)
			{
				return; // Already inactive.
			}

			Active = false;
		}

		public void Update(float delta)
		{
			// Simulate emotion fluids.
			emotion = emotion.Simulate(Vector8.One, personality.Vector8, settings.emotionSimulation * delta);

			// Dampen emotions.
			emotion = emotion.Lerp(Vector8.One, settings.emotionDamping * delta);

			// Dampen stimuli.
			foreach (KeyValuePair<IEntity, Vector8> kvp in stimuli)
			{
				stimuli[kvp.Key] = kvp.Value.Lerp(Vector8.Zero, settings.emotionDamping * delta);
			}
		}

		public void Stimulate(Vector8 stimulation, IEntity source = null)
		{
			if (source != null)
			{
				// An entity is responsible for this stimulation, add to stimuli.
				stimuli[source] += stimulation;
			}
			else
			{
				// No particular entity is responsible for this stimulation, add to emotion.
				emotion += stimulation;
			}
		}
	}
}
