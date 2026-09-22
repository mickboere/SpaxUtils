using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SpaxUtils
{
	/// <summary>
	/// Top-priority global volume with an in-memory profile, so settings can override post effects without touching scene profiles.
	/// </summary>
	public class SettingsVolume : IService, IDisposable
	{
		private const float PRIORITY = 1000f;

		private readonly GameObject gameObject;
		private readonly VolumeProfile profile;

		public SettingsVolume()
		{
			profile = ScriptableObject.CreateInstance<VolumeProfile>();
			profile.name = nameof(SettingsVolume);

			gameObject = new GameObject($"[{nameof(SettingsVolume)}]");
			if (Application.isPlaying)
			{
				Object.DontDestroyOnLoad(gameObject);
			}

			Volume volume = gameObject.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = PRIORITY;
			volume.sharedProfile = profile;
		}

		public void Dispose()
		{
			Object.Destroy(gameObject);
			Object.Destroy(profile);
		}

		/// <summary>
		/// Returns the override component of type <typeparamref name="T"/>, adding it when missing.
		/// </summary>
		public T Get<T>() where T : VolumeComponent
		{
			if (!profile.TryGet(out T component))
			{
				component = profile.Add<T>();
			}
			return component;
		}

		/// <summary>
		/// Overrides <paramref name="parameter"/> with <paramref name="value"/> while <paramref name="active"/>, else releases it.
		/// </summary>
		public static void Override<T>(VolumeParameter<T> parameter, bool active, T value)
		{
			parameter.overrideState = active;
			if (active)
			{
				parameter.value = value;
			}
		}
	}
}
