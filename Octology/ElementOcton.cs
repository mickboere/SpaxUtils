using System;

namespace SpaxUtils
{
	/// <summary>
	/// Class that wraps around a <see cref="Vector8"/>, assigning an element to each member.
	/// Each element has their own characteristics and dynamics with other elements.
	/// </summary>
	[Serializable]
	public class ElementOcton
	{
		/// <summary>
		/// The underlying <see cref="SpaxUtils.Vector8"/> this class wraps around.
		/// </summary>
		public Vector8 Vector8;

		/// <summary>
		/// Lies between Daeth and Light, opposite of Water.
		/// Masculine head, strong.
		/// Emotion of Anger, motivation is Action, manifests in Energy.
		/// </summary>
		public float Fire { get { return Vector8.N; } set { Vector8.N = value; } }

		/// <summary>
		/// Lies between Fire and Air, opposite of Nature.
		/// Masculine tip into Feminine root; smart.
		/// Emotion of Anticipation, motivation is Continuation, manifests in Current.
		/// </summary>
		public float Light { get { return Vector8.NE; } set { Vector8.NE = value; } }

		/// <summary>
		/// Lies between Light and Daeth, opposite of Earth.
		/// Feminine base; soft.
		/// Emotion of Happiness, motivation is Giving, manifests in Ether.
		/// </summary>
		public float Air { get { return Vector8.E; } set { Vector8.E = value; } }

		/// <summary>
		/// Lies between Air and Water, opposite of Daeth.
		/// Feminine limb; bound.
		/// Emotion of Acceptance, motivation is Proximity, manifests in Spirit.
		/// </summary>
		public float Faeth { get { return Vector8.SE; } set { Vector8.SE = value; } }

		/// <summary>
		/// Lies between Daeth and Nature, opposite of Fire.
		/// Feminine head; liquid.
		/// Emotion of Fear, motivation is Thought, manifests in Flow.
		/// </summary>
		public float Water { get { return Vector8.S; } set { Vector8.S = value; } }

		/// <summary>
		/// Lies between Water and Earth, opposite of Light.
		/// Feminine tip into Masculine root; growth.
		/// Emotion of Surprise, motivation is Newness, manifests in Essence.
		/// </summary>
		public float Nature { get { return Vector8.SW; } set { Vector8.SW = value; } }

		/// <summary>
		/// Lies between Nature and Daeth, oppposite of Air.
		/// Masculine base, solid.
		/// Emotion of Sadness, motivation is Getting, manifests in Matter.
		/// </summary>
		public float Earth { get { return Vector8.W; } set { Vector8.W = value; } }

		/// <summary>
		/// Lies between Earth and Fire, opposite of Daeth.
		/// Masculine limb, sharp.
		/// Emotion of Disgust, motivation is Distance, manifests in Space.
		/// </summary>
		public float Daeth { get { return Vector8.NW; } set { Vector8.NW = value; } }

		public ElementOcton(float fire, float light, float air, float faeth, float water, float nature, float earth, float daeth)
		{
			this.Fire = fire;
			this.Light = light;
			this.Air = air;
			this.Faeth = faeth;
			this.Water = water;
			this.Nature = nature;
			this.Earth = earth;
			this.Daeth = daeth;
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
			return $"(Fire={Fire}, Light={Light}, Air={Air}, Faeth={Faeth}, Water={Water}, Nature={Nature}, Earth={Earth}, Daeth={Daeth})";
		}
	}
}
