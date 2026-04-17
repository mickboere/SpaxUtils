using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

namespace SpaxUtils
{
	/// <summary>
	/// Scene-side component that wraps a <see cref="PlayableDirector"/> and registers itself
	/// with <see cref="CutsceneService"/> under a const string key so that level-flow nodes
	/// can trigger cutscene playback without direct scene references.
	/// Binds the <see cref="CinemachineBrain"/> from <see cref="CameraManager"/> at runtime
	/// since the main camera is not a scene object.
	/// Implements <see cref="INotificationReceiver"/> to dispatch <see cref="AgentCommandMarker"/>
	/// notifications to agents via their communication channels.
	/// </summary>
	public class CutsceneDirector : MonoBehaviour, INotificationReceiver
	{
		/// <summary>
		/// The underlying <see cref="PlayableDirector"/> with all scene track bindings configured in the inspector.
		/// </summary>
		public PlayableDirector Director => director;

		/// <summary>
		/// Whether the timeline already fades to black as its final frame.
		/// When true, the <see cref="CutsceneService"/> skips its own fade-to-black
		/// and does an instant handoff instead.
		/// </summary>
		public bool EndsOnBlack => endsOnBlack;

		[SerializeField] private PlayableDirector director;
		[SerializeField] private string cutsceneKey;

		[Header("Fade")]
		[SerializeField, Tooltip("Enable if the timeline's last frame is already fully black.")] private bool endsOnBlack;
		[SerializeField, Tooltip("Optional fade overlay CanvasGroup used by the timeline. Reset to transparent on cleanup.")] private CanvasGroup fadeOverlay;

		private CameraManager cameraManager;
		private EntityService entityService;
		private HashSet<string> occupiedAgentIds = new HashSet<string>();

		private void Awake()
		{
			if (director == null)
			{
				director = GetComponent<PlayableDirector>();
			}

			cameraManager = GlobalDependencyManager.Instance.Get<CameraManager>();
			entityService = GlobalDependencyManager.Instance.Get<EntityService>();

			// Bind the persistent CinemachineBrain to any Cinemachine tracks on the timeline.
			if (cameraManager != null && cameraManager.Brain != null)
			{
				BindCinemachineBrain(cameraManager.Brain);
			}

			// Register as notification receiver once the playable graph is created.
			director.played += OnDirectorPlayed;

			CutsceneService cutsceneService = GlobalDependencyManager.Instance.Get<CutsceneService>();
			cutsceneService.Register(cutsceneKey, this);
		}

		private void OnDestroy()
		{
			if (director != null)
			{
				director.played -= OnDirectorPlayed;
			}

			if (!GlobalDependencyManager.HasInstance)
			{
				return;
			}

			CutsceneService cutsceneService = GlobalDependencyManager.Instance.Get<CutsceneService>(true, false);
			if (cutsceneService != null)
			{
				cutsceneService.Unregister(cutsceneKey);
			}
		}

		/// <summary>
		/// Resets any scene-side state owned by this director (e.g. fade overlay, camera blend).
		/// Called by <see cref="CutsceneService"/> during completion.
		/// </summary>
		public void Cleanup()
		{
			if (fadeOverlay != null)
			{
				fadeOverlay.alpha = 0f;
			}

			// Cancel any active Cinemachine blend so the next vcam cuts in instantly.
			if (cameraManager != null && cameraManager.Brain != null)
			{
				cameraManager.Brain.ActiveBlend = null;
			}

			// Auto-vacate any agents that were commanded to occupy a POI during the cutscene.
			if (entityService != null && occupiedAgentIds.Count > 0)
			{
				foreach (string agentId in occupiedAgentIds)
				{
					IAgent agent = entityService.Get<IAgent>(agentId);
					if (agent != null)
					{
						agent.Comms.Send(new AgentCommandMsg(AgentCommand.VacatePOI));
					}
				}
				occupiedAgentIds.Clear();
			}
		}

		/// <summary>
		/// Receives <see cref="INotification"/> events from timeline markers.
		/// Dispatches <see cref="AgentCommandMarker"/> data to the target agent's comms channel.
		/// </summary>
		public void OnNotify(Playable origin, INotification notification, object context)
		{
			if (notification is AgentCommandMarker marker)
			{
				if (entityService == null)
				{
					SpaxDebug.Error("CutsceneDirector: EntityService is null, cannot dispatch agent command.");
					return;
				}

				IAgent agent = entityService.Get<IAgent>(marker.AgentId);
				if (agent == null)
				{
					SpaxDebug.Error("CutsceneDirector: Agent not found.", marker.AgentId);
					return;
				}

				agent.Comms.Send(new AgentCommandMsg(marker.Command, marker.Parameter, marker.Immediate));

				// Track agents that received OccupyPOI so we can auto-vacate in Cleanup.
				if (marker.Command == AgentCommand.OccupyPOI)
				{
					occupiedAgentIds.Add(marker.AgentId);
				}
				else if (marker.Command == AgentCommand.VacatePOI)
				{
					occupiedAgentIds.Remove(marker.AgentId);
				}
			}
		}

		/// <summary>
		/// Registers this director as a notification receiver on all playable outputs
		/// so that markers on any track are received.
		/// </summary>
		private void OnDirectorPlayed(PlayableDirector dir)
		{
			if (!dir.playableGraph.IsValid())
			{
				return;
			}

			int outputCount = dir.playableGraph.GetOutputCount();
			for (int i = 0; i < outputCount; i++)
			{
				dir.playableGraph.GetOutput(i).AddNotificationReceiver(this);
			}
		}

		/// <summary>
		/// Finds all Cinemachine tracks in the timeline and binds the given <see cref="CinemachineBrain"/> to them.
		/// </summary>
		private void BindCinemachineBrain(CinemachineBrain brain)
		{
			TimelineAsset timeline = director.playableAsset as TimelineAsset;
			if (timeline == null)
			{
				return;
			}

			foreach (TrackAsset track in timeline.GetOutputTracks())
			{
				// CinemachineTrack outputs expect a CinemachineBrain binding.
				if (track is CinemachineTrack)
				{
					director.SetGenericBinding(track, brain);
				}
			}
		}
	}
}
