using UnityEngine;

namespace SpaxUtils
{
	public class AgentAudioHandler : AgentComponentBase
	{
		[SerializeField] private AgentAudioProfile profile;
		[SerializeField] private float distanceMultiplier = 1f;

		private AgentStatHandler agentStatHandler;
		private Pool<PooledAudioSource> audioPool;

		private float lastHealth;

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

		public void PlayExertion(float intensity, float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetExertionSFX(intensity);
			sfx?.Play(audioPool.Request(Agent.Targetable.Point, Agent.Transform).AudioSourceWrapper, volume, distance * distanceMultiplier);
		}

		public void PlayDamage(float intensity, float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetDamageSFX(intensity);
			sfx?.Play(audioPool.Request(Agent.Targetable.Point, Agent.Transform).AudioSourceWrapper, volume, distance * distanceMultiplier);
		}

		public void PlayDeath(float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetDeathSFX();
			sfx?.Play(audioPool.Request(Agent.Targetable.Point, Agent.Transform).AudioSourceWrapper, volume, distance * distanceMultiplier);
		}

		public void PlaySatisfy(float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetSatisfySFX();
			sfx?.Play(audioPool.Request(Agent.Targetable.Point, Agent.Transform).AudioSourceWrapper, volume, distance * distanceMultiplier);
		}

		public void PlayAction(string act, float volume = 1f, float distance = 1f)
		{
			if (profile == null)
			{
				return;
			}

			SFXData sfx = profile.GetActionSFX(act);
			sfx?.Play(audioPool.Request(Agent.Targetable.Point, Agent.Transform).AudioSourceWrapper, volume, distance * distanceMultiplier);
		}

		#endregion Public Methods

		#region Private Methods

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
