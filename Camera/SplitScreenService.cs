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

		/// <summary>
		/// Index of the player whose view swallows the other while merged, or -1.
		/// </summary>
		public int MergeOwner { get; private set; } = -1;

		/// <summary>
		/// How far the divider has wiped towards <see cref="MergeOwner"/> taking the whole screen, 0-1.
		/// </summary>
		public float Merge { get; private set; }

		/// <summary>
		/// Whether the wipe completed and <see cref="MergeOwner"/> holds the whole screen.
		/// </summary>
		public bool IsMerged => MergeOwner >= 0 && Merge >= 1f;

		private readonly object fullscreenLock = new object();
		private readonly CameraManager cameraManager;
		private readonly PlayerInputService playerInputService;
		private readonly CallbackService callbackService;

		private float mergeTarget;
		private float mergeSpeed;

		public SplitScreenService(CameraManager cameraManager, PlayerInputService playerInputService,
			CallbackService callbackService)
		{
			this.cameraManager = cameraManager;
			this.playerInputService = playerInputService;
			this.callbackService = callbackService;
			cameraManager.PlayerCamerasChangedEvent += Relayout;
			callbackService.UpdateCallback += OnUpdate;
		}

		public void Dispose()
		{
			cameraManager.PlayerCamerasChangedEvent -= Relayout;
			callbackService.UpdateCallback -= OnUpdate;
		}

		/// <summary>
		/// Wipes the divider away over <paramref name="duration"/> until player <paramref name="playerIndex"/>
		/// holds the whole screen. Only has effect with exactly two player cameras.
		/// </summary>
		public void MergeInto(int playerIndex, float duration)
		{
			if (MergeOwner != playerIndex)
			{
				MergeOwner = playerIndex;
				Merge = 0f;
			}
			mergeTarget = 1f;
			mergeSpeed = duration > 0f ? 1f / duration : float.MaxValue;
			Relayout();
		}

		/// <summary>
		/// Wipes the divider back in over <paramref name="duration"/>, restoring the regular split.
		/// </summary>
		public void Unmerge(float duration)
		{
			if (MergeOwner < 0)
			{
				return;
			}
			if (Merge <= 0f)
			{
				MergeOwner = -1;
			}
			mergeTarget = 0f;
			mergeSpeed = duration > 0f ? 1f / duration : float.MaxValue;
			Relayout();
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

			// The merge owner left, or there are no longer exactly two views to merge.
			if (MergeOwner >= 0 && (players.Count != 2 || !players.Exists((p) => p.Key == MergeOwner)))
			{
				MergeOwner = -1;
				Merge = 0f;
				mergeTarget = 0f;
			}
			int mergePosition = players.FindIndex((p) => p.Key == MergeOwner);

			for (int i = 0; i < players.Count; i++)
			{
				bool owner = players[i].Key == FullscreenOwner;
				Rect rect = owner ? new Rect(0f, 0f, 1f, 1f) : mergePosition >= 0 ?
					CalculateMergedViewport(i, mergePosition, Merge, Layout) :
					CalculateViewport(i, players.Count, Layout);
				bool visible = FullscreenOwner >= 0 ? owner : rect.width > 0.001f && rect.height > 0.001f;
				Apply(players[i].Value.Camera, rect, visible);
			}

			LayoutChangedEvent?.Invoke();
		}

		private void OnUpdate()
		{
			if (MergeOwner < 0 || Merge == mergeTarget)
			{
				return;
			}

			// Unscaled, so a pause can't freeze the wipe halfway.
			Merge = Mathf.MoveTowards(Merge, mergeTarget, mergeSpeed * Time.unscaledDeltaTime);
			if (Merge <= 0f && mergeTarget <= 0f)
			{
				MergeOwner = -1;
			}
			Relayout();
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

		/// <summary>
		/// Two-view split whose divider slides away from the view at <paramref name="ownerPosition"/>.
		/// </summary>
		private static Rect CalculateMergedViewport(int position, int ownerPosition, float merge, SplitLayout layout)
		{
			// Divider runs from the middle to the far edge of the non-owner, the owner's side of which is position 0.
			float divider = Mathf.Lerp(0.5f, ownerPosition == 0 ? 1f : 0f, merge);
			if (layout == SplitLayout.Horizontal)
			{
				// Position 0 is on top, so its share is measured down from the top edge.
				return position == 0 ?
					new Rect(0f, 1f - divider, 1f, divider) :
					new Rect(0f, 0f, 1f, 1f - divider);
			}
			return position == 0 ?
				new Rect(0f, 0f, divider, 1f) :
				new Rect(divider, 0f, 1f - divider, 1f);
		}

		private static void Apply(Camera camera, Rect rect, bool visible)
		{
			camera.enabled = visible;

			// A wiped-away view keeps its last rect: canvases sized from a zero rect produce invalid (NaN) bounds.
			if (!visible && (rect.width <= 0.001f || rect.height <= 0.001f))
			{
				return;
			}
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
