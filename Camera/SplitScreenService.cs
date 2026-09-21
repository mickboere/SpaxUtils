using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpaxUtils
{
	public enum SplitLayout
	{
		Horizontal, // Players stacked top to bottom.
		Vertical // Players side by side.
	}

	/// <summary>
	/// Divides the screen between player cameras, and hands the whole screen to one player while they own a pausing menu.
	/// </summary>
	public class SplitScreenService : IService, IDisposable
	{
		// Above the menus' exclusive request (1), so a locked player's own menus can't override the lock.
		private const int LOCK_PRIORITY = 2;

		/// <summary>
		/// Invoked after the viewports were reapplied: players joined or left, or the layout or fullscreen owner changed.
		/// </summary>
		public event Action LayoutChangedEvent;

		/// <summary>
		/// Whether more than one player camera is active.
		/// </summary>
		public bool IsSplit { get; private set; }

		public SplitLayout Layout { get; private set; } = SplitLayout.Horizontal;

		/// <summary>
		/// Index of the player that currently owns the full screen, or -1.
		/// </summary>
		public int FullscreenOwner { get; private set; } = -1;

		private readonly object fullscreenLock = new object();
		private readonly CameraManager cameraManager;
		private readonly PlayerInputService playerInputService;

		public SplitScreenService(CameraManager cameraManager, PlayerInputService playerInputService)
		{
			this.cameraManager = cameraManager;
			this.playerInputService = playerInputService;
			cameraManager.PlayerCamerasChangedEvent += Relayout;
		}

		public void Dispose()
		{
			cameraManager.PlayerCamerasChangedEvent -= Relayout;
		}

		public void SetLayout(SplitLayout layout)
		{
			Layout = layout;
			Relayout();
		}

		/// <summary>
		/// Gives the whole screen to <paramref name="wrapper"/>'s player while split.
		/// Others keep only <paramref name="lockActionMap"/>. False when another player already owns it.
		/// </summary>
		public bool TryClaimFullscreen(PlayerInputWrapper wrapper, string lockActionMap)
		{
			int index = playerInputService.GetPlayerIndex(wrapper);
			if (index < 0 || !IsSplit || FullscreenOwner == index)
			{
				return true;
			}
			if (FullscreenOwner >= 0)
			{
				return false;
			}

			FullscreenOwner = index;
			if (!string.IsNullOrEmpty(lockActionMap))
			{
				foreach (KeyValuePair<int, PlayerInputWrapper> other in playerInputService.Wrappers)
				{
					if (other.Key != index && other.Value != null)
					{
						other.Value.RequestActionMaps(fullscreenLock, LOCK_PRIORITY, lockActionMap);
					}
				}
			}

			Relayout();
			return true;
		}

		/// <summary>
		/// Returns the full screen if <paramref name="wrapper"/>'s player owns it.
		/// </summary>
		public void ReleaseFullscreen(PlayerInputWrapper wrapper)
		{
			int index = playerInputService.GetPlayerIndex(wrapper);
			if (index >= 0 && index == FullscreenOwner)
			{
				ClearFullscreen();
				Relayout();
			}
		}

		/// <summary>
		/// Reapplies every player camera's viewport, including the overlay cameras stacked on it.
		/// </summary>
		public void Relayout()
		{
			List<KeyValuePair<int, MainCameraHandler>> players = GetPlayers();
			IsSplit = players.Count > 1;

			// The owner left or split-screen ended while they held the screen.
			if (FullscreenOwner >= 0 && (players.Count < 2 || !players.Exists((p) => p.Key == FullscreenOwner)))
			{
				ClearFullscreen();
			}

			for (int i = 0; i < players.Count; i++)
			{
				bool owner = players[i].Key == FullscreenOwner;
				Rect rect = owner ? new Rect(0f, 0f, 1f, 1f) : CalculateViewport(i, players.Count, Layout);
				Apply(players[i].Value.Camera, rect, FullscreenOwner < 0 || owner);
			}

			LayoutChangedEvent?.Invoke();
		}

		private void ClearFullscreen()
		{
			FullscreenOwner = -1;
			foreach (PlayerInputWrapper wrapper in playerInputService.Wrappers.Values)
			{
				if (wrapper != null)
				{
					wrapper.CompleteActionMapRequest(fullscreenLock);
				}
			}
		}

		private List<KeyValuePair<int, MainCameraHandler>> GetPlayers()
		{
			List<KeyValuePair<int, MainCameraHandler>> players = new List<KeyValuePair<int, MainCameraHandler>>();
			foreach (KeyValuePair<int, MainCameraHandler> player in cameraManager.PlayerCameras)
			{
				if (player.Value != null && player.Value.Camera != null)
				{
					players.Add(player);
				}
			}
			return players;
		}

		private static Rect CalculateViewport(int position, int count, SplitLayout layout)
		{
			if (count <= 1)
			{
				return new Rect(0f, 0f, 1f, 1f);
			}

			float size = 1f / count;
			return layout == SplitLayout.Horizontal ?
				new Rect(0f, 1f - (position + 1) * size, 1f, size) : // First player on top.
				new Rect(position * size, 0f, size, 1f); // First player on the left.
		}

		private static void Apply(Camera camera, Rect rect, bool visible)
		{
			camera.enabled = visible;
			camera.rect = rect;

			// Overlays render into the base viewport, but a Screen Space - Camera canvas
			// sizes itself from its own camera's rect.
			foreach (Camera overlay in camera.GetUniversalAdditionalCameraData().cameraStack)
			{
				if (overlay != null)
				{
					overlay.rect = rect;
				}
			}
		}
	}
}
