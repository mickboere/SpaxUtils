using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that wraps around a <see cref="SpaxUtils.Vector8"/>, assigning an element to each member.
	/// Each element has their own characteristics and dynamics with other elements.
	/// </summary>
	[Serializable]
	public class ElementOcton : IOcton
	{
		public Vector8 Vector8 => new Vector8(fire, light, air, faeth, water, nature, earth, daeth);

		/// <summary>
		/// Lies between Daeth and Light, opposite of Water.
		/// Masculine head, strong.
		/// Emotion of Anger, motivation is Action, manifests in Energy.
		/// </summary>
		[SerializeField] private float fire;

		/// <summary>
		/// Lies between Fire and Air, opposite of Nature.
		/// Masculine tip into Feminine root; smart.
		/// Emotion of Anticipation, motivation is Continuation, manifests in Current.
		/// </summary>
		[SerializeField] private float light;

		/// <summary>
		/// Lies between Light and Daeth, opposite of Earth.
		/// Feminine base; soft.
		/// Emotion of Happiness, motivation is Giving, manifests in Ether.
		/// </summary>
		[SerializeField] private float air;

		/// <summary>
		/// Lies between Air and Water, opposite of Daeth.
		/// Feminine limb; bound.
		/// Emotion of Acceptance, motivation is Proximity, manifests in Spirit.
		/// </summary>
		[SerializeField] private float faeth;

		/// <summary>
		/// Lies between Daeth and Nature, opposite of Fire.
		/// Feminine head; liquid.
		/// Emotion of Fear, motivation is Thought, manifests in Flow.
		/// </summary>
		[SerializeField] private float water;

		/// <summary>
		/// Lies between Water and Earth, opposite of Light.
		/// Feminine tip into Masculine root; growth.
		/// Emotion of Surprise, motivation is Newness, manifests in Essence.
		/// </summary>
		[SerializeField] private float nature;

		/// <summary>
		/// Lies between Nature and Daeth, oppposite of Air.
		/// Masculine base, solid.
		/// Emotion of Sadness, motivation is Getting, manifests in Matter.
		/// </summary>
		[SerializeField] private float earth;

		/// <summary>
		/// Lies between Earth and Fire, opposite of Daeth.
		/// Masculine limb, sharp.
		/// Emotion of Disgust, motivation is Distance, manifests in Space.
		/// </summary>
		[SerializeField] private float daeth;

		public ElementOcton(float fire, float light, float air, float faeth, float water, float nature, float earth, float daeth)
		{
			this.fire = fire;
			this.light = light;
			this.air = air;
			this.faeth = faeth;
			this.water = water;
			this.nature = nature;
			this.earth = earth;
			this.daeth = daeth;
		}

		public static implicit operator Vector8(ElementOcton octon)
		{
			return octon.Vector8;
		}

		/// <summary>
		/// Returns a new Vector8 with the calculated effectiveness of each element when paired against their opposite element.
		/// </summary>
		//public static Vector8 GetEffectiveness(this Vector8 vector)
		//{
		//	return new Vector8(
		//		CalculateEffectiveness(vector.N, vector.S),
		//		CalculateEffectiveness(Light, Nature),
		//		CalculateEffectiveness(Air, Earth),
		//		CalculateEffectiveness(Faeth, Daeth),
		//		CalculateEffectiveness(Water, Fire),
		//		CalculateEffectiveness(Nature, Light),
		//		CalculateEffectiveness(Earth, Air),
		//		CalculateEffectiveness(Daeth, Faeth));
		//}

		public static float CalculateEffectiveness(float a, float b)
		{
			return a / (a + b);
		}

		public override string ToString()
		{
			return $"(Fire={fire}, Light={light}, Air={air}, Faeth={faeth}, Water={water}, Nature={nature}, Earth={earth}, Daeth={daeth})";
		}
	}
}
