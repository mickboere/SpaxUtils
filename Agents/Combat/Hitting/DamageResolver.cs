using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// What a strike carries into the damage pipeline, detached from the hit that delivers it so the
	/// AI can resolve a strike it has not thrown yet.
	/// </summary>
	public readonly struct StrikeData
	{
		public readonly float Slash;
		public readonly float Power;
		public readonly float Pierce;
		public readonly float PowerBand;
		public readonly float ForceBand;
		public readonly float LimbMass;
		public readonly float HitterMass;
		public readonly float BodyMassFraction;
		public readonly float Rank;
		public readonly float Luck;

		public StrikeData(float slash, float power, float pierce, float powerBand, float forceBand,
			float limbMass, float hitterMass, float bodyMassFraction, float rank, float luck)
		{
			Slash = slash;
			Power = power;
			Pierce = pierce;
			PowerBand = powerBand;
			ForceBand = forceBand;
			LimbMass = limbMass;
			HitterMass = hitterMass;
			BodyMassFraction = Mathf.Clamp01(bodyMassFraction);
			Rank = rank;
			Luck = luck;
		}

		public StrikeData(HitData hitData) : this(hitData.Slash, hitData.Power, hitData.Pierce,
			hitData.PowerBand, hitData.ForceBand, hitData.LimbMass, hitData.HitterMass,
			hitData.BodyMassFraction, hitData.Rank, hitData.Luck)
		{ }
	}

	/// <summary>
	/// The defending side of the pipeline: the two walls plus the anatomy shaping blunt and crit.
	/// Vulnerability is passed in already rear-exposure adjusted.
	/// </summary>
	public readonly struct DefenceData
	{
		public readonly float Armor;
		public readonly float Yield;
		public readonly float Hardness;
		public readonly float Vulnerability;
		public readonly float Luck;

		/// <summary>Centre-octad defence: what blunt and force are walled by.</summary>
		public float MeanDefence => (Armor + Yield) * 0.5f;

		public DefenceData(float armor, float yield, float hardness, float vulnerability, float luck)
		{
			Armor = armor;
			Yield = yield;
			Hardness = hardness;
			Vulnerability = vulnerability;
			Luck = luck;
		}
	}

	/// <summary>
	/// Everything one resolved strike produces. Crit is returned as odds plus the damage a crit WOULD do,
	/// so the hit rolls it while an estimate takes its expectation.
	/// </summary>
	public readonly struct DamageResult
	{
		public readonly float Slash;
		public readonly float Pierce;
		/// <summary>Pierce as if unguarded — what a crit lands with, and the guard-break basis.</summary>
		public readonly float PierceOpen;
		public readonly float Blunt;
		public readonly float CritChance;
		/// <summary>Damage a crit would add; 0 chance means it cannot happen, not that it costs nothing.</summary>
		public readonly float CritDamage;
		public readonly float Penetration;
		public readonly float Coupling;
		public readonly float BluntEffectiveness;
		public readonly float BluntWall;
		public readonly float Impact;
		public readonly float Force;
		/// <summary>Endurance wear from force alone, before the damage share is added.</summary>
		public readonly float Stagger;
		public readonly float Guard;

		private readonly float staggerWeight;

		public DamageResult(float slash, float pierce, float pierceOpen, float blunt, float critChance,
			float critDamage, float penetration, float coupling, float bluntEffectiveness, float bluntWall,
			float impact, float force, float stagger, float guard, float staggerWeight)
		{
			Slash = slash;
			Pierce = pierce;
			PierceOpen = pierceOpen;
			Blunt = blunt;
			CritChance = critChance;
			CritDamage = critDamage;
			Penetration = penetration;
			Coupling = coupling;
			BluntEffectiveness = bluntEffectiveness;
			BluntWall = bluntWall;
			Impact = impact;
			Force = force;
			Stagger = stagger;
			Guard = guard;
			this.staggerWeight = staggerWeight;
		}

		/// <summary>Crit damage weighted by its odds — what an estimate should expect to take.</summary>
		public float ExpectedCrit => CritChance * CritDamage;

		/// <summary>Total physics damage with <paramref name="critDamage"/> included (pre-guard cancellation).</summary>
		public float TotalWith(float critDamage) => Slash + Pierce + Blunt + critDamage;

		/// <summary>What reaches health: the total less the blunt the guard cancelled.</summary>
		public float HealthWith(float critDamage) => Mathf.Max(0f, TotalWith(critDamage) - Blunt * Guard);

		/// <summary>What endurance takes: the damage share at post-guard rates, plus the force's stagger.</summary>
		public float EnduranceWith(float critDamage) => staggerWeight * (Slash + Pierce + critDamage) + Stagger;

		public float ExpectedTotal => TotalWith(ExpectedCrit);
		public float ExpectedHealth => HealthWith(ExpectedCrit);
		public float ExpectedEndurance => EnduranceWith(ExpectedCrit);
	}

	/// <summary>
	/// THE damage pipeline: guard, the two contests, the three channels, impact and force. The hit handler
	/// resolves real hits through it and the AI resolves hypothetical ones, so neither can drift.
	/// </summary>
	public static class DamageResolver
	{
		/// <summary>
		/// Resolves <paramref name="strike"/> against <paramref name="defence"/> behind a guard of
		/// <paramref name="guard"/> (0-1). A <paramref name="neglect"/>ed hit (block/parry) still
		/// carries its momentum, so only the damage channels are voided.
		/// </summary>
		public static DamageResult Resolve(StrikeData strike, DefenceData defence, float guard,
			CombatSettings settings, bool neglect = false)
		{
			guard = Mathf.Clamp01(guard);
			float armor = defence.Armor;
			float yieldStat = defence.Yield;
			float critPivot = settings == null ? 0f : settings.CritPivot;
			float contestPower = settings == null ? 1f : settings.ContestPower;

			// --- GUARD ---
			// Guard (Earth) flattens edges: the full Armor, shield included, comes off the Slash.
			// Points only run the wall twice and never reach zero; dashing (Air) is their real counter.
			float slashIn = neglect ? 0f : Mathf.Max(0f, strike.Slash - armor * guard);
			float couplingOpen = SpaxFormulas.CalculateCoupling(strike.Pierce, yieldStat, critPivot);
			float pierceIn = Mathf.Lerp(strike.Pierce, strike.Pierce * couplingOpen, guard);

			// --- CONTESTS ---
			// Edge vs Armor, point vs Yield, on what guard let through; whatever neither takes lands as blunt below.
			float band = strike.PowerBand;
			float penetration = SpaxFormulas.Transfer(slashIn, armor);
			float coupling = SpaxFormulas.CalculateCoupling(pierceIn, yieldStat, critPivot);

			// Each flank needs the power band behind it to get past the OTHER wall.
			float slashDrive = SpaxFormulas.Transfer(band, yieldStat);
			float pierceDrive = SpaxFormulas.Transfer(band, armor);

			// --- SLASH ---
			float slashDamage = slashIn * SpaxFormulas.Contests(penetration, slashDrive, contestPower);

			// --- PIERCE & CRIT ---
			// A crit found a gap: its odds come from the guarded point, but it lands with the unguarded one.
			float pierceOpen = neglect ? 0f : strike.Pierce * SpaxFormulas.Contests(couplingOpen, pierceDrive, contestPower);
			float pierceDamage = neglect ? 0f : pierceIn * SpaxFormulas.Contests(coupling, pierceDrive, contestPower);
			float critChance = neglect || strike.Pierce <= 0f
				? 0f
				: SpaxFormulas.CalculateCritChance(coupling, defence.Vulnerability, strike.Luck, defence.Luck);
			float critDamage = pierceOpen * (settings == null ? 1f : settings.CritMultiplier);

			// --- BLUNT ---
			// What neither cut nor caught, carried by the whole power band. Centre-octad, so walled by the mean.
			float blunt = ResolveBlunt(band, penetration, coupling, defence, settings,
				out float bluntEffectiveness, out float bluntWall);
			float bluntDamage = neglect ? 0f : blunt;

			// Momentum TRANSMITTED: what the own wall refused made the contact rigid, and guard stiffens the rest.
			// A failed drive didn't deliver, so it's excluded.
			float rigidity = 1f - (1f - guard) * bluntWall;
			float impact = Mathf.Lerp(bluntEffectiveness, 1f, rigidity);

			// The force band, scaled by mass as ratios so a heavy club outpushes a light one at any level.
			float force = strike.ForceBand * impact *
				(settings == null ? 1f : settings.ForceMassFactor(strike.LimbMass, strike.HitterMass,
					strike.BodyMassFraction, strike.Rank));
			float stagger = SpaxFormulas.CalculateDamage(force, defence.MeanDefence);

			return new DamageResult(slashDamage, pierceDamage, pierceOpen, bluntDamage, critChance, critDamage,
				penetration, coupling, bluntEffectiveness, bluntWall, impact, force, stagger, guard,
				settings == null ? 0f : settings.StaggerDamageWeight);
		}

		private static float ResolveBlunt(float band, float edge, float point, DefenceData defence,
			CombatSettings settings, out float effectiveness, out float wall)
		{
			float contestPower = settings == null ? 1f : settings.ContestPower;
			float mean = defence.MeanDefence;

			effectiveness = Mathf.Sqrt(Mathf.Clamp01(defence.Hardness) * Mathf.Clamp01((1f - edge) * (1f - point)));
			float offence = band * effectiveness * (settings == null ? 1f : settings.BluntScale);
			wall = SpaxFormulas.Transfer(offence, mean, settings == null ? 1f : settings.BluntWallExponent);

			return offence * SpaxFormulas.Contests(wall, SpaxFormulas.Transfer(band, mean), contestPower);
		}
	}
}
