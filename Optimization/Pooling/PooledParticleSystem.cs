using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(ParticleSystem))]
	public class PooledParticleSystem : PooledItemBase
	{
		public override bool Finished => !ParticleSystem.isPlaying;
		public override int DefaultPoolSize => defaultPoolSize;

		[field: SerializeField] public ParticleSystem ParticleSystem { get; private set; }
		[SerializeField] private int defaultPoolSize = 10;

		protected void Awake()
		{
			if (ParticleSystem == null)
			{
				ParticleSystem = GetComponent<ParticleSystem>();
			}
		}
	}
}
