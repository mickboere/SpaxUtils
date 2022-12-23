using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Float modifier that animates the input over time using an animation curve.
	/// </summary>
	public class TimedCurveModifier : FloatModifierBase
	{
		public override ModMethod Method => method;

		private ModMethod method;
		private AnimationCurve curve;
		private Timer timer;
		private CallbackService callbackService;

		public TimedCurveModifier(ModMethod method, AnimationCurve curve, Timer timer, CallbackService callbackService)
		{
			this.method = method;
			this.curve = curve;
			this.timer = timer;
			this.callbackService = callbackService;

			callbackService.UpdateCallback += OnUpdate;
		}

		public override void Dispose()
		{
			callbackService.UpdateCallback -= OnUpdate;
			base.Dispose();
		}

		private void OnUpdate()
		{
			if (!timer)
			{
				Dispose();
			}
			else
			{
				OnModChanged();
			}
		}

		public override float Modify(float input)
		{
			return input * curve.Evaluate(timer.Progress);
		}
	}
}
