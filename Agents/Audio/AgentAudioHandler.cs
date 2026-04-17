using UnityEngine;

namespace SpaxUtils
{
	public class AgentAudioHandler : AgentComponentBase
	{
		[SerializeField] private AgentAudioProfile profile;
		[SerializeField] private float distanceMultiplier = 1f;

		private AgentStatHandler agentStatHandler;
		private Pool<PooledAudioSource> audioPool;

		private float pitch;
		private float lastHealth;
		private PooledAudioSource audioSource;

		public void InjectDependencies(AgentStatHandler agentStatHandler, Pool<PooledAudioSource> audioPool, [Optional] AgentAudioProfile audioProfile)
		{
			this.agentStatHandler = agentStatHandler;
			this.audioPool = audioPool;

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
				lastHealth = agentStatHandler.PointStats.SW.Current;
				agentStatHandler.PointStats.SW.Current.ValueChangedEvent += OnHealthChangedEvent;
				Agent.DiedEvent += OnDiedEvent;
			}
		}

		protected void OnDisable()
		{
			agentStatHandler.PointStats.SW.Current.ValueChangedEvent -= OnHealthChangedEvent;
			Agent.DiedEvent -= OnDiedEvent;
		}

		#region Public Methods

		public void Play(SFXData sfx, float volume = 1f, float distance = 1f)
		{
			if (sfx == null)
			{
				return;
			}

			// Permit only 1 sound to play at a time.
			if (audioSource == null)
			{
				audioSource = audioPool.Request(Agent.Targetable.Point, Agent.Transform);
				audioSource.AudioSourceWrapper.SetEntityTimeScale(EntityTimeScale);
				audioSource.OnDisableEvent += OnASWDisabled;
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

			SFXData sfx = profile.GetDeathSFX();
			Play(sfx, volume, distance);
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
			float current = agentStatHandler.PointStats.SW.Current;
			float damage = lastHealth - current;
			if (damage > 0f && current > 0f)
			{
				float fraction = damage / agentStatHandler.PointStats.SW.Max;
				PlayDamage(fraction);
			}
			lastHealth = agentStatHandler.PointStats.SW.Current;
		}

		private void OnDiedEvent(DeathContext deathContext)
		{
			PlayDeath();
		}

		#endregion Private Methods
	}
}
