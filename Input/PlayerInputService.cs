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
		private readonly Dictionary<int, PlayerInputWrapper> wrappersByIndex = new Dictionary<int, PlayerInputWrapper>();

		public PlayerInputWrapper GetOrCreate(int playerIndex, InputActionAsset actions, Camera camera = null)
		{
			if (wrappersByIndex.TryGetValue(playerIndex, out PlayerInputWrapper wrapper) && wrapper != null)
			{
				wrapper.SetCamera(camera);
				return wrapper;
			}

			// Create new wrapper (non-persistent by default) and make it persistent here.
			wrapper = PlayerInputWrapper.Create(actions, camera);
			GameObject.DontDestroyOnLoad(wrapper.gameObject);

			// Ensure the dictionary does not hold a destroyed Unity ref.
			wrappersByIndex[playerIndex] = wrapper;

			// Best-effort: ensure the wrapper is actually using the desired index.
			// If this ever logs, something else created a PlayerInput and grabbed index 0 first.
			if (wrapper.PlayerIndex != playerIndex)
			{
				SpaxDebug.Error("PlayerInputService", $"Created wrapper index mismatch. Requested={playerIndex}, Got={wrapper.PlayerIndex}");
			}

			return wrapper;
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
	}
}
