using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Main <see cref="IPoser"/> class implementation that provides many options for defining a 4-way pose blend.
	/// </summary>
	public class Poser : IPoser, IDisposable
	{
		/// <inheritdoc/>
		public PoseInstructions[] Instructions => new PoseInstructions[]
		{
			new PoseInstructions(From, 1f - Interpolation),
			new PoseInstructions(To, Interpolation)
		};

		public PoserSettings Settings { get; }

		public PoseTransition From { get; private set; }
		public PoseTransition To { get; private set; }
		public float Interpolation { get; private set; }

		public bool Transitioning => transitionTimer.HasValue && !transitionTimer.Value.Expired;
		public float TransitionPrecisionModifier { get; set; } = 1f;

		private PoseSequence fromSequence;
		private PoseSequence toSequence;
		private float fromTime;
		private float toTime;

		private TimerStruct? fromTimer;
		private TimerStruct? toTimer;
		private TimerStruct? transitionTimer;

		private CallbackService callbackService;

		public Poser(CallbackService callbackService, PoserSettings settings)
		{
			Settings = settings;
			this.callbackService = callbackService;

			callbackService.UpdateCallback += OnUpdate;
		}

		public void Dispose()
		{
			callbackService.UpdateCallback -= OnUpdate;
			Stop();
		}

		private void OnUpdate()
		{
			// Sequences
			if (toTimer.HasValue && toSequence != null)
			{
				// Sequence Transitions
				if (Transitioning)
				{
					if (fromTimer.HasValue && fromSequence != null)
					{
						Pose(fromSequence, fromTimer.Value.Time, toSequence, toTimer.Value.Time, transitionTimer.Value.Progress, false);
					}
					else
					{
						// Pose to sequence transition.
						Pose(From, toSequence, toTimer.Value.Time, transitionTimer.Value.Progress);
					}
				}
				// Regular sequences
				else if (!transitionTimer.HasValue)
				{
					Pose(toSequence, toTimer.Value.Time);
				}
			}
			// Pose to pose Transitions
			else if (Transitioning)
			{
				Pose(From, To, transitionTimer.Value.Progress);
			}
		}

		public void Pose(PoseTransition poseTransition)
		{
			From = default;
			To = poseTransition;
			Interpolation = 1f;
		}

		public void Pose(PoseTransition from, PoseTransition to, float interpolation)
		{
			From = from;
			To = to;
			Interpolation = interpolation;
		}

		public void Pose(PoseSequence sequence, float time)
		{
			toSequence = sequence;
			toTime = time;
			PoseTransition transitionPose = sequence.Evaluate(time);
			Pose(transitionPose);
		}

		public void PoseNormalized(PoseSequence sequence, float progress)
		{
			Pose(sequence, sequence.TotalDuration * progress);
		}

		public void Pose(PoseSequence fromSequence, float fromTime, PoseSequence toSequence, float toTime, float transition, bool timeIsNormalized)
		{
			this.fromSequence = fromSequence;
			this.fromTime = timeIsNormalized ? fromTime * fromSequence.TotalDuration : fromTime;
			this.toSequence = toSequence;
			this.toTime = timeIsNormalized ? toTime * toSequence.TotalDuration : toTime;

			PoseTransition fromPose = fromSequence.Evaluate(this.fromTime);
			PoseTransition toPose = toSequence.Evaluate(this.toTime);

			Pose(fromPose, toPose, transition);
		}

		public void Pose(PoseTransition fromPose, PoseSequence toSequence, float toTime, float transition)
		{
			this.toSequence = toSequence;
			PoseTransition toPose = toSequence.Evaluate(toTime);
			Pose(fromPose, toPose, transition);
		}

		public void PoseNormalized(PoseTransition fromPose, PoseSequence toSequence, float toProgress, float transition)
		{
			Pose(fromPose, toSequence, toSequence.TotalDuration * toProgress, transition);
		}

		public void Play(PoseSequence sequence, float timeOffset = 0f, float speed = 1f)
		{
			transitionTimer = null;
			fromSequence = null;
			fromTimer = null;
			toSequence = sequence;
			toTimer = new TimerStruct(sequence.TotalDuration, timeOffset, speed);
			Pose(sequence, 0f);
		}

		public void Transition(object controller, PoseSequence fromSequence, float fromOffset, float fromSpeed, PoseSequence toSequence, float toOffset, float toSpeed, float transitionDuration)
		{
			this.fromSequence = fromSequence;
			fromTimer = new TimerStruct(fromSequence.TotalDuration, fromOffset, fromSpeed);
			this.toSequence = toSequence;
			toTimer = new TimerStruct(toSequence.TotalDuration, toOffset, toSpeed);
			transitionTimer = new TimerStruct(transitionDuration);
			Pose(fromSequence, fromOffset, toSequence, toOffset, 0f, false);
		}

		public void Transition(PoseSequence toSequence, float transitionDuration, float timeOffset = 0f, float speed = 1f)
		{
			// If transitioning from one transition to another, select the correct sequence to transition from.
			if (this.toSequence != null)
			{
				if (fromSequence == null || !Transitioning || transitionTimer.Value.Progress > 0.5f)
				{
					fromSequence = this.toSequence;
					if (toTimer.HasValue)
					{
						fromTimer = toTimer.Value;
					}
				}
			}
			else if (Transitioning && transitionTimer.Value.Progress > 0.5f)
			{
				// There is no sequence to reference, use the previous pose as starting point.
				From = To;
			}

			this.toSequence = toSequence;
			toTimer = new TimerStruct(toSequence.TotalDuration, timeOffset, speed);
			transitionTimer = new TimerStruct(transitionDuration);
			// Pose happens in update.
		}

		public void Transition(PoseTransition fromPose, PoseTransition toPose, float transitionDuration)
		{
			fromSequence = null;
			fromTimer = null;
			toSequence = null;
			toTimer = null;
			transitionTimer = new TimerStruct(transitionDuration);
			Pose(fromPose, toPose, 0f);
		}

		public void Transition(PoseTransition fromPose, PoseSequence toSequence, float transitionDuration, float timeOffset = 0f, float speed = 1f)
		{
			fromSequence = null;
			fromTimer = null;
			this.toSequence = toSequence;
			toTimer = new TimerStruct(toSequence.TotalDuration, timeOffset, speed);
			transitionTimer = new TimerStruct(transitionDuration);
			PoseTransition toPose = toSequence.Evaluate(timeOffset);
			Pose(fromPose, toSequence, timeOffset, toPose.Transition);
		}

		public void Pause()
		{
			toTimer.Value.Pause();
		}

		public void Continue()
		{
			toTimer.Value.Continue();
		}

		public void Stop()
		{
			fromSequence = null;
			fromTimer = null;
			toSequence = null;
			toTimer = null;
			transitionTimer = null;
		}

		public void Reset()
		{
			Stop();
			Pose(default(PoseTransition));
		}

		#region Data Evaluation

		public bool TryEvaluateFloat(string identifier, float defaultIfNull, out float result)
		{
			bool fromSuccess = From.TryEvaluateFloat(identifier, defaultIfNull, out float from);
			bool toSuccess = To.TryEvaluateFloat(identifier, defaultIfNull, out float to);

			result = Mathf.Lerp(from, to, Interpolation);
			return fromSuccess && toSuccess;
		}

		public bool TryEvaluateBool(string identifier, bool defaultIfNull, out bool result)
		{
			bool fromSuccess = From.TryEvaluateBool(identifier, defaultIfNull, out bool from);
			bool toSuccess = To.TryEvaluateBool(identifier, defaultIfNull, out bool to);

			result = Interpolation < 0.5f ? from : to;
			return fromSuccess && toSuccess;
		}

		#endregion
	}
}
