using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ScreenShaker : EntityComponentMono
	{
		private const float OFFSET = 1000f;
		private const float SPECTRE = 0.57142857142f;

		[SerializeField] private float maxAngle = 10f;
		[SerializeField] private ShakeSettings defaultSettings;
		[SerializeField] private bool test;

		private CameraWrapper cameraHandler;
		private AgentSenseComponent awarenessComponent;

		private List<IShakeSource> sources = new List<IShakeSource>();

		public void InjectDependencies(CameraWrapper cameraHandler, AgentSenseComponent awarenessComponent)
		{
			this.cameraHandler = cameraHandler;
			this.awarenessComponent = awarenessComponent;
		}

		protected void OnValidate()
		{
			if (Application.isPlaying)
			{
				if (test)
				{
					Shake(defaultSettings);
					test = false;
				}
			}
		}

		protected void OnEnable()
		{
			awarenessComponent.ImpactEvent += OnImpactEvent;
		}

		protected void OnDisable()
		{
			awarenessComponent.ImpactEvent -= OnImpactEvent;
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
		/// Apply a new screenshake source using provided <paramref name="settings"/>.
		/// </summary>
		public void Shake(ShakeSettings settings)
		{
			Shake(new ShakeSource(settings.Magnitude, settings.Frequency, settings.Duration, settings.Falloff));
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
				float noiseX = GenerateNoise(0f, source);
				float noiseY = GenerateNoise(OFFSET, source);
				angles += new Vector3(-source.Magnitude.y + noiseY, source.Magnitude.x + noiseX, 0f) * e;
			}

			cameraHandler.Cam.transform.localEulerAngles = angles.Clamp(-maxAngle, maxAngle);
		}

		private float GenerateNoise(float offset, IShakeSource source)
		{
			Random.InitState(source.Seed);
			float off = Random.value * OFFSET + offset;
			float a = Mathf.PerlinNoise1D(off + source.Time * source.Frequency).Clamp01().Remap(-1f, 1f) * source.Magnitude.z * SPECTRE;
			float b = Mathf.PerlinNoise1D(off * 2f + source.Time * source.Frequency * 2f).Clamp01().Remap(-1f, 1f) * source.Magnitude.z * SPECTRE * 0.5f;
			float c = Mathf.PerlinNoise1D(off * 4f + source.Time * source.Frequency * 4f).Clamp01().Remap(-1f, 1f) * source.Magnitude.z * SPECTRE * 0.25f;
			return a + b + c;
		}

		/// <summary>
		/// Converts impact data to a screenshake.
		/// </summary>
		private void OnImpactEvent(ImpactData impact)
		{
			Vector3 magnitude = cameraHandler.transform.InverseTransformDirection(impact.Direction).normalized.Multiply(defaultSettings.Magnitude);
			float multiplier = impact.Force > 1f ? Mathf.Log10(impact.Force) : 1f;
			Shake(magnitude * multiplier, defaultSettings.Frequency * multiplier, defaultSettings.Duration * multiplier, defaultSettings.Falloff);
		}
	}
}
