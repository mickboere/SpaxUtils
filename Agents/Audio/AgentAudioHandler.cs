using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentAudioHandler : AgentComponentBase
	{
		[SerializeField] private AgentAudioProfile profile;
		[SerializeField] private float distanceMultiplier = 1f;

		// Disabled FOR NOW while hunting the warped hit sounds; flip back on to restore timescaled agent audio.
		private static readonly bool timescaleAudio = false;

		private AgentStatHandler agentStatHandler;
		private Pool<PooledAudioSource> audioPool;

		private AgentHitHandlerComponent hitHandler;

		private float pitch;
		private float lastHealth;
		private float pendingDamage;

		// One event may span several layers, but a new event still cuts off the one before it.
		private List<PooledAudioSource> eventSources = new List<PooledAudioSource>();

		private const float STRAIN_FADE = 0.1f;
		private AudioSourceWrapper strainSource;
		private object strainOwner;

		/// <summary>Whether a strain holds the voice; nothing else is voiced over it.</summary>
		public bool Straining => strainSource != null;

		public void InjectDependencies(AgentStatHandler agentStatHandler, Pool<PooledAudioSource> audioPool,
			[Optional] AgentAudioProfile audioProfile, [Optional] AgentHitHandlerComponent hitHandler)
		{
			this.agentStatHandler = agentStatHandler;
			this.audioPool = audioPool;
			this.hitHandler = hitHandler;

			if (audioProfile != null)
			{
				profile = audioProfile;
			}
		}

		protected void OnEnable()
		{
			if (profile != null)
			{
				pitch = Agent.RuntimeData.GetValue(EntityDataIdentifiers.AUDIO_PITCH, 1f);
				lastHealth = agentStatHandler.ResourceStats.SW.Current;
				agentStatHandler.ResourceStats.SW.Current.ValueChangedEvent += OnHealthChangedEvent;
				Agent.DiedEvent += OnDiedEvent;
			}
		}

		protected void OnDisable()
		{
			agentStatHandler.ResourceStats.SW.Current.ValueChangedEvent -= OnHealthChangedEvent;
			Agent.DiedEvent -= OnDiedEvent;
			pendingDamage = 0f;
			eventSources.Clear();
			EndStrain(0f);
		}

		protected void Update()
		{
			// The damage grunt is the receiving end's tail: it waits for the hit-pause to lift.
			if (pendingDamage > 0f && (hitHandler == null || hitHandler.HitPauseRemaining <= 0f))
			{
				PlayDamage(pendingDamage);
				pendingDamage = 0f;
			}
		}

		#region Public Methods

		/// <param name="scaledTime">Whether the entity's local timescale bends this sound's pitch.</param>
		public void Play(SFXData sfx, float volume = 1f, float distance = 1f, bool scaledTime = true)
		{
			if (sfx == null || Straining)
			{
				return;
			}

			BeginEvent();
			sfx.Play(RequestSource(scaledTime), volume, pitch, distance * distanceMultiplier);
		}

		/// <param name="scaledTime">Whether the entity's local timescale bends this sound's pitch.</param>
		public void Play(TieredSFX tiered, float intensity, float volume = 1f, float distance = 1f, bool scaledTime = true)
		{
			if (tiered == null || !tiered.HasTiers || Straining)
			{
				return;
			}

			BeginEvent();
			tiered.Play(intensity, () => RequestSource(scaledTime), volume, pitch, distance * distanceMultiplier);
		}

		public void PlayExertion(float intensity, float volume = 1f, float distance = 1f)
		{
			if (profile != null)
			{
				Play(profile.Exertion, intensity, volume, distance);
			}
		}

		public void PlayDamage(float intensity, float volume = 1f, float distance = 1f)
		{
			if (profile != null)
			{
				Play(profile.Damage, intensity, volume, distance);
			}
		}

		public void PlayDeath(float volume = 1f, float distance = 1f)
		{
			if (profile != null)
			{
				// Death breaks any strain. Exempt from the death timescale lerp to 0, which would drag the cry down.
				EndStrain(STRAIN_FADE);
				Play(profile.GetDeathSFX(), volume, distance, false);
			}
		}

		public void PlaySatisfy(float volume = 1f, float distance = 1f)
		{
			if (profile != null)
			{
				Play(profile.GetSatisfySFX(), volume, distance);
			}
		}

		public void PlayAction(string act, float volume = 1f, float distance = 1f)
		{
			if (profile != null)
			{
				Play(profile.GetActionSFX(act), volume, distance);
			}
		}

		/// <summary>
		/// Voices the sustained strain at <paramref name="stretch"/> (0..1 of the charge), starting it on first call.
		/// Only its current <paramref name="owner"/> can stop it.
		/// </summary>
		public void Strain(object owner, float stretch)
		{
			SFXData sfx = profile != null ? profile.Strain : null;
			if (sfx == null || sfx.Clips == null || sfx.Clips.Count == 0)
			{
				return;
			}

			if (strainSource == null)
			{
				// Takes the voice over from any grunt still sounding.
				BeginEvent();
				AudioSourceWrapper source = ClaimSource(true).AudioSourceWrapper;
				sfx.PlayLoop(source, randomStart: true, volume: 0f, distance: distanceMultiplier);
				if (!source.IsPlaying)
				{
					// Never held idle: the pool would hand it to someone else while we still write to it.
					return;
				}
				strainSource = source;
				strainSource.FadeIn(STRAIN_FADE);
			}

			strainOwner = owner;
			strainSource.Volume.BaseValue = sfx.VolumeRange.Lerp(stretch);
			strainSource.Pitch.BaseValue = pitch * sfx.PitchRange.Lerp(stretch);
		}

		public void StopStrain(object owner)
		{
			if (owner == strainOwner)
			{
				EndStrain(STRAIN_FADE);
			}
		}

		#endregion Public Methods

		#region Private Methods

		/// <summary>Silences the previous event, so only one sound speaks for this agent at a time.</summary>
		private void BeginEvent()
		{
			for (int i = eventSources.Count - 1; i >= 0; i--)
			{
				eventSources[i].AudioSourceWrapper.Stop();
			}

			eventSources.Clear();
		}

		private void EndStrain(float fade)
		{
			if (strainSource == null)
			{
				return;
			}

			if (fade > 0f)
			{
				strainSource.FadeOut(fade, EasingMethod.InOutSine);
			}
			else
			{
				strainSource.Stop();
			}

			strainSource = null;
			strainOwner = null;
		}

		private AudioSourceWrapper RequestSource(bool scaledTime)
		{
			PooledAudioSource source = ClaimSource(scaledTime);

			// Dropped the moment the pool reclaims it, so a later event never stops someone else's sound.
			void OnSourceDisabled()
			{
				source.OnDisableEvent -= OnSourceDisabled;
				eventSources.Remove(source);
			}

			source.OnDisableEvent += OnSourceDisabled;
			eventSources.Add(source);

			return source.AudioSourceWrapper;
		}

		private PooledAudioSource ClaimSource(bool scaledTime)
		{
			PooledAudioSource source = audioPool.Request(Agent.Targetable.Point, Agent.Transform);

			// The source is shared between invokes, so the timescale link is decided per play.
			if (scaledTime && timescaleAudio)
			{
				source.AudioSourceWrapper.SetEntityTimeScale(EntityTimeScale);
			}
			else
			{
				source.AudioSourceWrapper.ClearEntityTimeScale();
			}

			return source;
		}

		private void OnHealthChangedEvent()
		{
			float current = agentStatHandler.ResourceStats.SW.Current;
			float damage = lastHealth - current;
			if (damage > 0f && current > 0f)
			{
				// Queued, not played: the hit-pause is only applied after the health drain that got us here.
				pendingDamage = Mathf.Max(pendingDamage, damage / agentStatHandler.ResourceStats.SW.Max);
			}
			lastHealth = agentStatHandler.ResourceStats.SW.Current;
		}

		private void OnDiedEvent(DeathContext deathContext)
		{
			// A queued grunt would cut the death cry off on the shared source.
			pendingDamage = 0f;
			PlayDeath();
		}

		#endregion Private Methods
	}
}
