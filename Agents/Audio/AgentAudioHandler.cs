using UnityEngine;

namespace SpaxUtils
{
	public class AgentAudioHandler : AgentComponentBase
	{
		[SerializeField] private AgentAudioProfile profile;
		[SerializeField] private float distanceMultiplier = 1f;

		private AgentStatHandler agentStatHandler;
		private Pool<PooledAudioSource> audioPool;

		private AgentHitHandlerComponent hitHandler;

		private float pitch;
		private float lastHealth;
		private float pendingDamage;
		private PooledAudioSource audioSource;

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
			if (sfx == null)
			{
				return;
			}

			// Permit only 1 sound to play at a time.
			if (audioSource == null)
			{
				audioSource = audioPool.Request(Agent.Targetable.Point, Agent.Transform);
				audioSource.OnDisableEvent += OnASWDisabled;
			}

			// The source is shared between invokes, so the timescale link is decided per play.
			if (scaledTime)
			{
				audioSource.AudioSourceWrapper.SetEntityTimeScale(EntityTimeScale);
			}
			else
			{
				audioSource.AudioSourceWrapper.ClearEntityTimeScale();
			}

			sfx.Play(audioSource.AudioSourceWrapper, volume, pitch, distance * distanceMultiplier);
		}

		public void PlayExertion(float intensity, float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetExertionSFX(intensity);
			Play(sfx, volume, distance);
		}

		public void PlayDamage(float intensity, float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetDamageSFX(intensity);
			Play(sfx, volume, distance);
		}

		public void PlayDeath(float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			// Exempt from the death timescale lerp to 0, which would drag the cry down with it.
			SFXData sfx = profile.GetDeathSFX();
			Play(sfx, volume, distance, false);
		}

		public void PlaySatisfy(float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetSatisfySFX();
			Play(sfx, volume, distance);
		}

		public void PlayAction(string act, float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetActionSFX(act);
			Play(sfx, volume, distance);
		}

		#endregion Public Methods

		#region Private Methods

		private void OnASWDisabled()
		{
			audioSource.OnDisableEvent -= OnASWDisabled;
			audioSource = null;
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
