using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IPooledItem"/> for audio sources.
	/// </summary>
	[RequireComponent(typeof(AudioSource))]
	public class PooledAudioSource : PooledItemBase
	{
		public override bool Finished => !AudioSource.isPlaying;
		public override int DefaultPoolSize => defaultPoolSize;

		[field: SerializeField] public AudioSource AudioSource { get; private set; }
		[SerializeField] private int defaultPoolSize = 25;


		protected void Awake()
		{
			if (AudioSource == null)
			{
				AudioSource = GetComponent<AudioSource>();
			}
		}
	}
}
