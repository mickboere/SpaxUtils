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
		private Vector3 current;
		private Vector3 velocity;

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

			current = Vector3.SmoothDamp(current, input, ref velocity, settings.Smoothing, settings.MaxSmoothingVelocity, deltaTime);
			return current;
		}
	}
}
