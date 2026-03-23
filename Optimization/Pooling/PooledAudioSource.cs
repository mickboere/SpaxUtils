using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IPooledItem"/> for audio sources.
	/// </summary>
	[RequireComponent(typeof(AudioSourceWrapper))]
	public class PooledAudioSource : PooledItemBase
	{
		public event Action OnDisableEvent;

		public override bool Finished => !AudioSourceWrapper.IsPlaying;
		public override int DefaultPoolSize => defaultPoolSize;
		public AudioSource AudioSource => AudioSourceWrapper.AudioSource;

		[field: SerializeField] public AudioSourceWrapper AudioSourceWrapper { get; private set; }
		[SerializeField] private int defaultPoolSize = 25;

		protected void Awake()
		{
			if (AudioSourceWrapper == null)
			{
				AudioSourceWrapper = GetComponent<AudioSourceWrapper>();
			}
		}

		protected void OnDisable()
		{
			// Reset the audio source to default settings upon returning to pool.
			if (AudioSourceWrapper != null)
			{
				AudioSourceWrapper.Reset();
			}

			OnDisableEvent?.Invoke();
		}
	}
}
