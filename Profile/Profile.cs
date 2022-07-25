namespace SpaxUtils
{
	/// <summary>
	/// Struct made up of eight different elements, each element with their own characteristics and inherent opposite element.
	/// </summary>
	public struct Profile
	{
		/// <summary>
		/// Lies between Dae and Lightning, opposite of Water.
		/// Emotion of Anger, motivation is Action, manifests in Energy.
		/// </summary>
		public float fire;

		/// <summary>
		/// Lies between Fae and Nature, opposite of Fire.
		/// Emotion of Fear, motivation is Thought, manifests in Flow.
		/// </summary>
		public float water;

		/// <summary>
		/// Lies between Nature and Dae, oppposite of Air.
		/// Emotion of Sadness, motivation is Getting, manifests in Matter.
		/// </summary>
		public float earth;

		/// <summary>
		/// Lies between Lightning and Fae, opposite of Earth.
		/// Emotion of Happiness, motivation is Giving, manifests in Ether.
		/// </summary>
		public float air;

		/// <summary>
		/// Lies between Water and Earth, opposite of Lightning.
		/// Emotion of Surprise, motivation is Newness, manifests in Essence.
		/// </summary>
		public float nature;

		/// <summary>
		/// Lies between Fire and Air, opposite of Nature.
		/// Emotion of Anticipation, motivation is Continuation, manifests in Current.
		/// </summary>
		public float lightning;

		/// <summary>
		/// Lies between Air and Water, opposite of Dae.
		/// Emotion of Acceptance, motivation is Proximity, manifests in Spirit.
		/// </summary>
		public float fae;

		/// <summary>
		/// Lies between Earth and Fire, opposite of Fae.
		/// Emotion of Disgust, motivation is Distance, manifests in Space.
		/// </summary>
		public float dae;

		/// <summary>
		/// The sum of all element values.
		/// </summary>
		public float Sum
		{
			get
			{
				return fire + water + earth + air + nature + lightning + fae + dae;
			}
		}

		/// <summary>
		/// Returns a new profile with the calculated effectiveness of each element when paired against their opposite element.
		/// </summary>
		public Profile Effectiveness
		{
			get
			{
				return new Profile(
					CalculateEffectiveness(fire, water),
					CalculateEffectiveness(water, fire),
					CalculateEffectiveness(earth, air),
					CalculateEffectiveness(air, earth),
					CalculateEffectiveness(nature, lightning),
					CalculateEffectiveness(lightning, nature),
					CalculateEffectiveness(fae, dae),
					CalculateEffectiveness(dae, fae));
			}
		}

		public Profile(float fire, float water, float earth, float air, float nature, float lightning, float fae, float dae)
		{
			this.fire = fire;
			this.water = water;
			this.earth = earth;
			this.air = air;
			this.nature = nature;
			this.lightning = lightning;
			this.fae = fae;
			this.dae = dae;
		}

		public float[] ToArray()
		{
			return new float[] { fire, water, earth, air, nature, lightning, fae, dae };
		}

		public static float CalculateEffectiveness(float a, float b)
		{
			return a / (a + b);
		}

		#region Operators

		public static Profile operator +(Profile a, Profile b)
		{
			return new Profile(
				a.fire + b.fire,
				a.water + b.water,
				a.earth + b.earth,
				a.air + b.air,
				a.nature + b.nature,
				a.lightning + b.lightning,
				a.fae + b.fae,
				a.dae + b.dae);
		}

		public static Profile operator -(Profile a, Profile b)
		{
			return new Profile(
				a.fire - b.fire,
				a.water - b.water,
				a.earth - b.earth,
				a.air - b.air,
				a.nature - b.nature,
				a.lightning - b.lightning,
				a.fae - b.fae,
				a.dae - b.dae);
		}

		public static Profile operator *(Profile a, Profile b)
		{
			return new Profile(
				a.fire * b.fire,
				a.water * b.water,
				a.earth * b.earth,
				a.air * b.air,
				a.nature * b.nature,
				a.lightning * b.lightning,
				a.fae * b.fae,
				a.dae * b.dae);
		}

		public static Profile operator /(Profile a, Profile b)
		{
			return new Profile(
				a.fire / b.fire,
				a.water / b.water,
				a.earth / b.earth,
				a.air / b.air,
				a.nature / b.nature,
				a.lightning / b.lightning,
				a.fae / b.fae,
				a.dae / b.dae);
		}

		public static Profile operator *(Profile a, float b)
		{
			return new Profile(
				a.fire * b,
				a.water * b,
				a.earth * b,
				a.air * b,
				a.nature * b,
				a.lightning * b,
				a.fae * b,
				a.dae * b);
		}

		public static Profile operator /(Profile a, float b)
		{
			return new Profile(
				a.fire / b,
				a.water / b,
				a.earth / b,
				a.air / b,
				a.nature / b,
				a.lightning / b,
				a.fae / b,
				a.dae / b);
		}

		#endregion

		public override string ToString()
		{
			return $"({fire}, {water}, {earth}, {air}, {nature}, {lightning}, {fae}, {dae})";
		}
	}
}
