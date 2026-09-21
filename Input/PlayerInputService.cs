using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaxUtils
{
	/// <summary>
	/// Global service that owns persistent PlayerInputWrapper instances by player index.
	/// Only this service should create wrappers.
	/// </summary>
	public class PlayerInputService : IService
	{
		/// <summary>
		/// All live wrappers, keyed by player index.
		/// </summary>
		public IReadOnlyDictionary<int, PlayerInputWrapper> Wrappers => wrappersByIndex;

		private readonly Dictionary<int, PlayerInputWrapper> wrappersByIndex = new Dictionary<int, PlayerInputWrapper>();

		public PlayerInputWrapper GetOrCreate(int playerIndex, InputActionAsset actions, Camera camera = null)
		{
			if (wrappersByIndex.TryGetValue(playerIndex, out PlayerInputWrapper wrapper) && wrapper != null)
			{
				wrapper.SetCamera(camera);
				return wrapper;
			}

			return Register(playerIndex, PlayerInputWrapper.Create(actions, camera));
		}

		/// <summary>
		/// Creates the wrapper for <paramref name="playerIndex"/> with <paramref name="devices"/> paired exclusively.
		/// </summary>
		public PlayerInputWrapper CreatePaired(int playerIndex, InputActionAsset actions, string controlScheme,
			params InputDevice[] devices)
		{
			Remove(playerIndex);
			return Register(playerIndex, PlayerInputWrapper.Create(actions, null, controlScheme, devices));
		}

		public bool TryGet(int playerIndex, out PlayerInputWrapper wrapper)
		{
			if (wrappersByIndex.TryGetValue(playerIndex, out wrapper) && wrapper != null)
			{
				return true;
			}

			wrapper = null;
			return false;
		}

		/// <summary>
		/// The player index <paramref name="wrapper"/> is registered under, or -1. The one source of a player's index.
		/// </summary>
		public int GetPlayerIndex(PlayerInputWrapper wrapper)
		{
			if (wrapper != null)
			{
				foreach (KeyValuePair<int, PlayerInputWrapper> kvp in wrappersByIndex)
				{
					if (kvp.Value == wrapper)
					{
						return kvp.Key;
					}
				}
			}
			return -1;
		}

		/// <summary>
		/// Destroys the wrapper of <paramref name="playerIndex"/>, unpairing its devices.
		/// </summary>
		public void Remove(int playerIndex)
		{
			if (wrappersByIndex.TryGetValue(playerIndex, out PlayerInputWrapper wrapper))
			{
				wrappersByIndex.Remove(playerIndex);
				if (wrapper != null)
				{
					// Deactivate first: PlayerInput frees its index and devices in OnDisable, Destroy is deferred.
					wrapper.gameObject.SetActive(false);
					Object.Destroy(wrapper.gameObject);
				}
			}
		}

		private PlayerInputWrapper Register(int playerIndex, PlayerInputWrapper wrapper)
		{
			GameObject.DontDestroyOnLoad(wrapper.gameObject);
			wrappersByIndex[playerIndex] = wrapper;

			// If this ever logs, something else created a PlayerInput and grabbed this index first.
			if (wrapper.PlayerIndex != playerIndex)
			{
				SpaxDebug.Error("PlayerInputService", $"Created wrapper index mismatch. Requested={playerIndex}, Got={wrapper.PlayerIndex}");
			}

			return wrapper;
		}
	}
}
