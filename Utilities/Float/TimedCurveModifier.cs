using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Float modifier that animates the input over time using an animation curve.
	/// </summary>
	public class TimedCurveModifier : FloatModifierBase, IDisposable
	{
		public override ModMethod Method => method;
		public TimerStruct Timer { get; }

		private ModMethod method;
		private AnimationCurve curve;
		private CallbackService callbackService;

		public TimedCurveModifier(ModMethod method, AnimationCurve curve, TimerStruct timer, CallbackService callbackService)
		{
			this.method = method;
			this.curve = curve;
			this.Timer = timer;
			this.callbackService = callbackService;

			callbackService.UpdateCallback += OnUpdate;
		}

		public void Dispose()
		{
			callbackService.UpdateCallback -= OnUpdate;
		}

		private void OnUpdate()
		{
			if (!Timer)
			{
				Dispose();
			}
			else
			{
				Dirty = true;
			}
		}

		public override void Applied()
		{
			// TimedCurveModifier is always dirty.
		}

		public override float Modify(float input)
		{
			return input * curve.Evaluate(Timer.Progress);
		}
	}
}
