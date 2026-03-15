using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that performs <see cref="AnimatedAction"/>s through the <see cref="IActor"/> system.
	/// Implements <see cref="IPerformer"/> directly (no helper class, one action at a time).
	/// Drives animation via the poser system or animator parameters depending on the action's configuration.
	/// Makes the agent's rigidbody kinematic during performance to prevent physics interference.
	/// </summary>
	public class AnimationPerformerComponent : EntityComponentMono, IPerformer
	{
		private const string PARAM_ACTION_INDEX = "ActionIndex";
		private const string PARAM_ACTION_ACTIVE = "ActionActive";
		private const string PARAM_ACTION_TIME = "ActionTime";

		/// <summary>
		/// Posing priority for animated actions. Above base moveset (0), below combat (10).
		/// </summary>
		private const int POSE_PRIORITY = 5;

		public event Action<IPerformer> StartedPreparingEvent;
		public event Action<IPerformer> StartedPerformingEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		/// <inheritdoc/>
		public int Priority => 0;

		/// <inheritdoc/>
		public IAct Act { get; private set; }

		/// <inheritdoc/>
		public PerformanceState State { get; private set; } = PerformanceState.Inactive;

		/// <inheritdoc/>
		public float RunTime { get; private set; }

		/// <inheritdoc/>
		public bool Paused { get; set; }

		/// <inheritdoc/>
		public bool Canceled { get; private set; }

		/// <inheritdoc/>
		public float CancelTime { get; private set; }

		/// <inheritdoc/>
		public float Weight { get; private set; }

		/// <summary>
		/// The currently active animated action, or null if idle.
		/// </summary>
		public AnimatedAction CurrentAction { get; private set; }

		[SerializeField, Tooltip("Configured animated actions this performer can execute.")]
		private List<AnimatedAction> actions;

		private AnimatorPoser animatorPoser;
		private AnimatorWrapper animatorWrapper;
		private RigidbodyWrapper rigidbodyWrapper;
		private CallbackService callbackService;
		private EntityStat timescale;

		//private FloatOperationModifier controlMod;
		private float fadeOutTime;
		private float cancelStartWeight;
		private bool subscribedToUpdate;
		private bool wasKinematic;

		public void InjectDependencies(IAgent agent, CallbackService callbackService,
			RigidbodyWrapper rigidbodyWrapper, AnimatorWrapper animatorWrapper,
			[Optional] AnimatorPoser animatorPoser)
		{
			this.callbackService = callbackService;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorWrapper = animatorWrapper;
			this.animatorPoser = animatorPoser;

			timescale = agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE);
		}

		protected void OnDestroy()
		{
			CleanupPerformance();
		}

		#region IPerformer

		/// <inheritdoc/>
		public bool SupportsAct(string act)
		{
			return FindAction(act) != null;
		}

		/// <inheritdoc/>
		public bool TryPrepare(IAct act, out IPerformer performer)
		{
			performer = null;

			// Cannot start a new action while one is active (excluding completed/inactive).
			if (State != PerformanceState.Inactive && State != PerformanceState.Completed)
			{
				return false;
			}

			AnimatedAction action = FindAction(act.Title);
			if (action == null)
			{
				return false;
			}

			// Start the performance.
			CurrentAction = action;
			Act = act;
			State = PerformanceState.Preparing;
			RunTime = 0f;
			Weight = 0f;
			CancelTime = 0f;
			Canceled = false;
			fadeOutTime = 0f;
			cancelStartWeight = 0f;

			// Set up control modifier to reduce movement during action.
			//controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			//rigidbodyWrapper.Control.AddModifier(this, controlMod);

			// Make rigidbody kinematic to prevent physics interference during the action.
			wasKinematic = rigidbodyWrapper.IsKinematic;
			rigidbodyWrapper.IsKinematic = true;

			// Subscribe to update loop.
			if (!subscribedToUpdate)
			{
				callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
				subscribedToUpdate = true;
			}

			performer = this;
			StartedPreparingEvent?.Invoke(this);
			return true;
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			// No charge mechanic; acknowledge that performance is underway.
			return State == PerformanceState.Preparing || State == PerformanceState.Performing;
		}

		/// <inheritdoc/>
		public bool TryCancel(bool force = false)
		{
			if (State == PerformanceState.Inactive || State == PerformanceState.Completed)
			{
				return false;
			}

			// Animated actions are always cancellable.
			Canceled = true;
			cancelStartWeight = Weight;
			return true;
		}

		#endregion IPerformer

		private void OnUpdate(float delta)
		{
			if (CurrentAction == null || State == PerformanceState.Inactive)
			{
				return;
			}

			float scaledDelta = delta * (timescale != null ? (float)timescale : 1f);

			if (!Canceled)
			{
				UpdateNormalPerformance(scaledDelta);
			}
			else
			{
				UpdateCanceledPerformance(scaledDelta);
			}

			// Drive animation.
			UpdateAnimation();

			// Update movement control reduction.
			//float control = 1f - (Weight);
			//controlMod.SetValue(control);

			// Broadcast update.
			PerformanceUpdateEvent?.Invoke(this);

			// Check for completion.
			if (State == PerformanceState.Completed)
			{
				Weight = 0f;
				PerformanceCompletedEvent?.Invoke(this);
				CleanupPerformance();
			}
		}

		private void UpdateNormalPerformance(float delta)
		{
			if (!Paused)
			{
				RunTime += delta;
			}

			if (State == PerformanceState.Preparing)
			{
				// Fade in: weight ramps 0 -> 1 over FadeInDuration.
				if (CurrentAction.FadeInDuration > 0f)
				{
					Weight = Mathf.Clamp01(RunTime / CurrentAction.FadeInDuration);
				}
				else
				{
					Weight = 1f;
				}

				if (RunTime >= CurrentAction.FadeInDuration)
				{
					State = PerformanceState.Performing;
					StartedPerformingEvent?.Invoke(this);
				}
			}

			if (State == PerformanceState.Performing)
			{
				Weight = 1f;

				if (!CurrentAction.Prolonged)
				{
					float effectiveDuration = CurrentAction.GetEffectiveDuration();
					// Total time before finishing = fade in + performance duration.
					float finishTime = CurrentAction.FadeInDuration + effectiveDuration;
					if (effectiveDuration > 0f && RunTime >= finishTime)
					{
						State = PerformanceState.Finishing;
						fadeOutTime = 0f;
					}
				}
				// Prolonged actions stay in Performing until cancelled.
			}

			if (State == PerformanceState.Finishing)
			{
				if (!Paused)
				{
					fadeOutTime += Time.deltaTime * (timescale != null ? (float)timescale : 1f);
				}

				if (CurrentAction.FadeOutDuration > 0f)
				{
					Weight = 1f - Mathf.Clamp01(fadeOutTime / CurrentAction.FadeOutDuration);
				}
				else
				{
					Weight = 0f;
				}

				if (fadeOutTime >= CurrentAction.FadeOutDuration)
				{
					State = PerformanceState.Completed;
				}
			}
		}

		private void UpdateCanceledPerformance(float delta)
		{
			State = PerformanceState.Finishing;

			CancelTime += delta;
			RunTime += delta;

			if (CurrentAction.FadeOutDuration > 0f)
			{
				// Fade from whatever weight we were at when cancelled.
				Weight = cancelStartWeight * (1f - Mathf.Clamp01(CancelTime / CurrentAction.FadeOutDuration));
			}
			else
			{
				Weight = 0f;
			}

			if (CancelTime >= CurrentAction.FadeOutDuration)
			{
				State = PerformanceState.Completed;
			}
		}

		private void UpdateAnimation()
		{
			switch (CurrentAction.AnimationType)
			{
				case PerformanceAnimationType.Poser:
					if (animatorPoser != null && CurrentAction.PosingData != null)
					{
						IPoserInstructions instructions = CurrentAction.PosingData.GetInstructions(RunTime);
						animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, instructions, POSE_PRIORITY, Weight);
					}
					break;

				case PerformanceAnimationType.Animator:
					animatorWrapper.SetInteger(PARAM_ACTION_INDEX, CurrentAction.AnimationIndex);
					animatorWrapper.SetBool(PARAM_ACTION_ACTIVE, State == PerformanceState.Preparing || State == PerformanceState.Performing);
					float effectiveDuration = CurrentAction.GetEffectiveDuration();
					float actionTime = effectiveDuration > 0f ? RunTime / effectiveDuration : 0f;
					animatorWrapper.SetFloat(PARAM_ACTION_TIME, actionTime);
					break;
			}
		}

		private void CleanupPerformance()
		{
			if (subscribedToUpdate)
			{
				callbackService.UnsubscribeUpdates(this);
				subscribedToUpdate = false;
			}

			//if (controlMod != null)
			//{
			//	rigidbodyWrapper.Control.RemoveModifier(this);
			//	controlMod = null;
			//}

			// Restore kinematic state.
			rigidbodyWrapper.IsKinematic = wasKinematic;

			if (animatorPoser != null)
			{
				animatorPoser.RevokeInstructions(this);
			}

			if (CurrentAction != null && CurrentAction.AnimationType == PerformanceAnimationType.Animator)
			{
				animatorWrapper.SetBool(PARAM_ACTION_ACTIVE, false);
			}

			CurrentAction = null;
			Act = null;
			State = PerformanceState.Inactive;
			Weight = 0f;
		}

		private AnimatedAction FindAction(string act)
		{
			if (actions == null)
			{
				return null;
			}

			for (int i = 0; i < actions.Count; i++)
			{
				if (actions[i] != null && actions[i].Identifier == act)
				{
					return actions[i];
				}
			}

			return null;
		}
	}
}
