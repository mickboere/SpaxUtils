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

		[field: SerializeField] public AudioSource AudioSource { get; private set; }

		protected void Awake()
		{
			if (AudioSource == null)
			{
				AudioSource = GetComponent<AudioSource>();
			}
		}
	}
}
