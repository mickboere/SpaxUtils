using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ScreenShaker : EntityComponentMono
	{
		[SerializeField] private float maxAngle = 10f;
		[Header("TESTING")]
		[SerializeField] private Vector3 testMagnitude;
		[SerializeField] private float testFrequency;
		[SerializeField] private float testDuration;
		[SerializeField] private AnimationCurve testFalloff;
		[SerializeField] private bool test;

		private CameraHandler cameraHandler;

		private List<IShakeSource> sources = new List<IShakeSource>();

		public void InjectDependencies(CameraHandler cameraHandler)
		{
			this.cameraHandler = cameraHandler;
		}

		protected void OnValidate()
		{
			if (Application.isPlaying)
			{
				if (test)
				{
					Shake(testMagnitude, testFrequency, testDuration, testFalloff);
					test = false;
				}
			}
		}

		protected void LateUpdate()
		{
			ApplyShake();
		}

		/// <summary>
		/// Apply a new screenshake source using provided parameters.
		/// </summary>
		public void Shake(Vector3 magnitude, float frequency, float duration, AnimationCurve falloff = null)
		{
			Shake(new ShakeSource(magnitude, frequency, duration, falloff));
		}

		/// <summary>
		/// Applies a new screenshake source.
		/// </summary>
		public void Shake(IShakeSource source)
		{
			sources.Add(source);
			//SpaxDebug.Log("SHAKE", $"mag={source.Magnitude}, freq={source.Frequency}");
		}

		private void ApplyShake()
		{
			Vector3 angles = new Vector3();
			for (int i = 0; i < sources.Count; i++)
			{
				// Remove and skip source if completed.
				if (sources[i].Completed)
				{
					sources[i].Dispose();
					sources.RemoveAt(i);
					i--;
					continue;
				}

				// Apply shake source.
				var source = sources[i];
				float e = source.Evaluate();
				float noiseX = Mathf.PerlinNoise1D(Time.time * source.Frequency).Remap(-1f, 1f) * source.Magnitude.z;
				float noiseY = Mathf.PerlinNoise1D(1000f + Time.time * source.Frequency).Remap(-1f, 1f) * source.Magnitude.z;
				angles += new Vector3(-source.Magnitude.y + noiseY, source.Magnitude.x + noiseX, 0f) * e;
			}

			cameraHandler.Cam.transform.localEulerAngles = angles.Clamp(-maxAngle, maxAngle);
		}
	}
}
