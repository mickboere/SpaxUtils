using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

namespace SpaxUtils
{
	/// <summary>
	/// Owns the <see cref="PlayableGraph"/> driving an agent: an existing controller on layer 0 and a mixer of
	/// <see cref="TimelinePlayer"/> slots on layer 1. Replaces the poser's per-rig controller entirely.
	/// </summary>
	// Natively the Animator evaluates between Update and LateUpdate, so anything reading the posed skeleton
	// in LateUpdate (FinalIK, Animation Rigging) is guaranteed fresh bones. Evaluating in LateUpdate at the
	// default order makes that a race, so this must come first among LateUpdates.
	[DefaultExecutionOrder(-10000)]
	[RequireComponent(typeof(Animator))]
	public class TimelineGraph : MonoBehaviour, IDependency
	{
		public bool IsBuilt => graph.IsValid();

		[SerializeField, Tooltip("Starting slot count. Grows on demand and never shrinks, so this is only a hint.")]
		private int initialSlots = 4;

		private Animator animator;
		private AnimatorWrapper animatorWrapper;
		private RigBuilder rigBuilder;
		private PlayableGraph graph;
		private AnimationLayerMixerPlayable layerMixer;
		private AnimationMixerPlayable performanceMixer;
		private TimelinePlayer[] players;

		public void InjectDependencies(AnimatorWrapper animatorWrapper)
		{
			this.animatorWrapper = animatorWrapper;
			animator = animatorWrapper == null ? GetComponent<Animator>() : animatorWrapper.Animator;
		}

		#region Slots

		/// <summary>
		/// Claims a slot for <paramref name="timeline"/>. Returns null when every slot is taken.
		/// The returned player owns its playhead; nothing advances until it is told to.
		/// </summary>
		public TimelinePlayer Play(AnimationTimeline timeline, float weight = 1f)
		{
			if (timeline == null || timeline.Clip == null)
			{
				SpaxDebug.Error("Cannot play timeline.", timeline == null ? "Timeline is null." : "Timeline has no clip.", this);
				return null;
			}

			if (!graph.IsValid())
			{
				Build();
			}

			int slot = FreeSlot();

			AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, timeline.Clip);
			playable.SetApplyFootIK(false);
			playable.SetSpeed(0d); // The playhead is the caller's; the clip must never self-advance.
			graph.Connect(playable, 0, performanceMixer, slot);

			players[slot] = new TimelinePlayer(timeline, playable, slot);
			players[slot].SetTime(0f, false);
			players[slot].Weight = weight;
			return players[slot];
		}

		/// <summary>Releases a slot and destroys its playable.</summary>
		public void Stop(TimelinePlayer player)
		{
			if (player == null || players == null || player.Slot < 0 || player.Slot >= players.Length)
			{
				return;
			}

			if (players[player.Slot] != player)
			{
				return;
			}

			if (graph.IsValid())
			{
				performanceMixer.SetInputWeight(player.Slot, 0f);
				graph.Disconnect(performanceMixer, player.Slot);
				if (player.Playable.IsValid())
				{
					player.Playable.Destroy();
				}
			}

			players[player.Slot] = null;
		}

		#endregion Slots

		protected void OnDestroy()
		{
			// Hand parameter access back before the playable dies under the wrapper.
			if (animatorWrapper != null)
			{
				animatorWrapper.ClearControllerPlayable();
			}

			if (graph.IsValid())
			{
				graph.Destroy();
			}
		}

		protected void LateUpdate()
		{
			// Built here rather than in Start: this component's execution order puts its Start ahead of
			// AnimatorPoser installing its override controller, and Build must read the FINAL controller.
			// By the first LateUpdate every Awake, OnEnable and Start has run.
			if (!graph.IsValid())
			{
				Build();
				if (!graph.IsValid())
				{
					return;
				}
			}

			UpdateWeights();

			// Rigging syncs its layers from scene values before we evaluate, per its own documented contract.
			if (rigBuilder != null)
			{
				rigBuilder.SyncLayers();
			}
			graph.Evaluate(Time.deltaTime);
		}

		#region Building

		private void Build()
		{
			if (graph.IsValid())
			{
				return;
			}

			if (animator == null)
			{
				animator = GetComponent<Animator>();
			}
			if (animatorWrapper == null)
			{
				animatorWrapper = gameObject.GetComponentRelative<AnimatorWrapper>();
			}
			rigBuilder = GetComponent<RigBuilder>();
			players = new TimelinePlayer[Mathf.Max(1, initialSlots)];

			graph = PlayableGraph.Create($"{name}_Timeline");
			graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

			performanceMixer = AnimationMixerPlayable.Create(graph, players.Length);
			layerMixer = AnimationLayerMixerPlayable.Create(graph, 2);

			// The Animator's own controller IS the base layer; there is nothing to configure separately.
			RuntimeAnimatorController controller = animator.runtimeAnimatorController;
			if (controller != null)
			{
				AnimatorControllerPlayable controllerPlayable = AnimatorControllerPlayable.Create(graph, controller);
				graph.Connect(controllerPlayable, 0, layerMixer, 0);
				layerMixer.SetInputWeight(0, 1f);

				// The playable holds its own copy of the state machine, so the wrapper has to write there
				// instead of to the Animator - otherwise every parameter set goes to a bypassed instance.
				if (animatorWrapper != null)
				{
					animatorWrapper.SetControllerPlayable(controllerPlayable);
				}
			}

			graph.Connect(performanceMixer, 0, layerMixer, 1);

			// Starts silent. UpdateWeights raises it only while something is actually playing.
			layerMixer.SetInputWeight(1, 0f);

			AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Timeline", animator);
			output.SetSourcePlayable(layerMixer);

			// Rigging appends its own outputs (PreviousInputs + high sorting order) into THIS graph, so
			// constraints evaluate on top of the pose we write. Nothing to chain manually.
			if (rigBuilder != null)
			{
				rigBuilder.Build(graph);
			}

			graph.Play();
		}

		#endregion Building

		/// <summary>
		/// Layer weight carries how much the performance overrides locomotion; mixer inputs carry only the
		/// ratio between concurrent moves. An idle performance layer MUST sit at zero - a mixer with no
		/// weight outputs the default pose rather than nothing, which would override the base layer with it.
		/// </summary>
		private void UpdateWeights()
		{
			float total = 0f;
			foreach (TimelinePlayer player in players)
			{
				if (player != null)
				{
					total += player.Weight;
				}
			}

			layerMixer.SetInputWeight(1, Mathf.Clamp01(total));

			float scale = total > 0f ? 1f / total : 0f;
			for (int i = 0; i < players.Length; i++)
			{
				performanceMixer.SetInputWeight(i, players[i] == null ? 0f : players[i].Weight * scale);
			}
		}

		/// <summary>
		/// Finds a free slot, doubling capacity when none is left. Slots are never released back, so growth
		/// settles at peak concurrency within the first few seconds and costs nothing thereafter.
		/// </summary>
		private int FreeSlot()
		{
			for (int i = 0; i < players.Length; i++)
			{
				if (players[i] == null)
				{
					return i;
				}
			}

			int previous = players.Length;
			System.Array.Resize(ref players, previous * 2);
			performanceMixer.SetInputCount(players.Length);
			return previous;
		}

	}
}
