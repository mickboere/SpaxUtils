using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that wraps around a <see cref="SpaxUtils.Vector8"/>, assigning an element to each member.
	/// Each element has their own characteristics and dynamics with other elements.
	/// </summary>
	[Serializable]
	public class ElementOctad : IOctad
	{
		public Vector8 Vector8 => new Vector8(fire, light, air, spirit, water, nature, earth, @void);

		/// <summary>
		/// Lies between Void and Light, opposite of Water.
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
		/// Lies between Light and Void, opposite of Earth.
		/// Feminine base; soft.
		/// Emotion of Happiness, motivation is Giving, manifests in Ether.
		/// </summary>
		[SerializeField] private float air;

		/// <summary>
		/// Lies between Air and Water, opposite of Void.
		/// Feminine limb; bound.
		/// Emotion of Acceptance, motivation is Proximity, manifests in Spirit.
		/// </summary>
		[SerializeField] private float spirit;

		/// <summary>
		/// Lies between Void and Nature, opposite of Fire.
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
		/// Lies between Nature and Void, opposite of Air.
		/// Masculine base, solid.
		/// Emotion of Sadness, motivation is Getting, manifests in Matter.
		/// </summary>
		[SerializeField] private float earth;

		/// <summary>
		/// Lies between Earth and Fire, opposite of Spirit.
		/// Masculine limb, sharp.
		/// Emotion of Disgust, motivation is Distance, manifests in Space.
		/// </summary>
		[SerializeField] private float @void;

		public ElementOctad(float fire, float light, float air, float spirit, float water, float nature, float earth, float @void)
		{
			this.fire = fire;
			this.light = light;
			this.air = air;
			this.spirit = spirit;
			this.water = water;
			this.nature = nature;
			this.earth = earth;
			this.@void = @void;
		}

		public static implicit operator Vector8(ElementOctad octon)
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
		//		CalculateEffectiveness(Spirit, Void),
		//		CalculateEffectiveness(Water, Fire),
		//		CalculateEffectiveness(Nature, Light),
		//		CalculateEffectiveness(Earth, Air),
		//		CalculateEffectiveness(Void, Spirit));
		//}

		public static float CalculateEffectiveness(float a, float b)
		{
			return a / (a + b);
		}

		public override string ToString()
		{
			return $"(Fire={fire}, Light={light}, Air={air}, Spirit={spirit}, Water={water}, Nature={nature}, Earth={earth}, Void={@void})";
		}
	}
}
