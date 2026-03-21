using UnityEngine;
using UnityEngine.Rendering;

namespace SpaxUtils
{
	/// <summary>
	/// Service that provides access to the global <see cref="VolumeProfile"/>.
	/// Register a profile via <see cref="Register(VolumeProfile)"/>, then retrieve
	/// any <see cref="VolumeComponent"/> through <see cref="TryGet{T}(out T)"/>.
	/// </summary>
	public class VolumeService : IService
	{
		public VolumeProfile Profile { get; private set; }

		/// <summary>
		/// Registers the global volume profile. Called by <see cref="VolumeRegistrant"/>.
		/// </summary>
		public void Register(VolumeProfile profile)
		{
			Profile = profile;
		}

		/// <summary>
		/// Unregisters the current profile.
		/// </summary>
		public void Unregister(VolumeProfile profile)
		{
			if (Profile == profile)
			{
				Profile = null;
			}
		}

		/// <summary>
		/// Attempts to retrieve a <see cref="VolumeComponent"/> of type <typeparamref name="T"/> from the registered profile.
		/// </summary>
		public bool TryGet<T>(out T component) where T : VolumeComponent
		{
			component = null;
			return Profile != null && Profile.TryGet(out component);
		}
	}
}
