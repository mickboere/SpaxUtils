using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class to smoothe user input depending on <see cref="MovementInputSettings"/>.
	/// </summary>
	public class MovementInputHelper : IDisposable
	{
		private MovementInputSettings settings;
		private Vector3 previousInput;
		private Vector3 current;
		private Vector3 shiftPoint;
		private float progress;

		public MovementInputHelper(MovementInputSettings settings)
		{
			this.settings = settings;
		}

		public void Dispose()
		{
		}

		public Vector2 Update(Vector2 input, float timeDelta)
		{
			Vector3 vector3 = Update(new Vector3(input.x, 0f, input.y), timeDelta);
			return new Vector2(vector3.x, vector3.z);
		}

		public Vector3 Update(Vector3 input, float deltaTime)
		{
			input = input.ApplyCurve(settings.InputRamp);

			if (input.magnitude < settings.MinimumInput)
			{
				input = Vector3.zero;
			}

			float inputDelta = (input - previousInput).magnitude / deltaTime;
			previousInput = input;
			if (inputDelta > settings.ChangeThreshold)
			{
				shiftPoint = current;
				progress = 0f;
			}

			//input *= settings.EvaluateInputRamp(input.magnitude);
			float accelerationAmount = (Vector3.Dot(current.normalized, input.normalized) + 1) * 0.5f;
			float distance = (input - shiftPoint).magnitude.Max(Mathf.Epsilon);
			float progressChange = 1f / Mathf.Lerp(settings.DecelerationTime, settings.AccelerationTime, accelerationAmount) * (1f / distance) * deltaTime;
			progress = Mathf.Clamp01(progress + progressChange);
			float eval = Mathf.Lerp(settings.EvaluateDecelerationNormalized(1f - progress), settings.EvaluateAccelerationNormalized(progress), accelerationAmount);
			current = Vector3.Lerp(shiftPoint, input, eval);

			if (float.IsNaN(current.x))
			{
				//SpaxDebug.Log("NaN!", $"MovementInputSmooth={MovementInputSmooth}, MovementInputRaw={MovementInputRaw}, input={input}");
				SpaxDebug.Log("NaN!", $"input={input}, current={current}, shift={shiftPoint}, progress={progress}, change={progressChange}, eval={eval}, accelerationAmount={accelerationAmount}, distance={distance}.");
			}

			return current;
		}
	}
}
