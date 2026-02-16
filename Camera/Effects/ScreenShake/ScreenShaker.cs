using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ScreenShaker : EntityComponentMono
	{
		private const float OFFSET = 1000f;
		private const float SPECTRE = 0.57142857142f;

		[SerializeField] private float maxAngle = 10f;
		[SerializeField] private float rollAmount = 1f;

		[SerializeField, Tooltip("Scales positional shake relative to the rotational shake output. " +
			"X/Y are directional bias, Z is rumble amplitude, matching IShakeSource.Magnitude semantics.")]
		private float positionMultiplier = 0.015f;

		[SerializeField] private ShakeSettings defaultSettings;
		[SerializeField] private bool test;

		private CinemachineShakeExtension shakeExtension;
		private AgentImpactHandler agentSenseComponent;

		private List<IShakeSource> sources = new List<IShakeSource>();

		public void InjectDependencies(CinemachineShakeExtension shakeExtension, AgentImpactHandler awarenessComponent)
		{
			this.shakeExtension = shakeExtension;
			this.agentSenseComponent = awarenessComponent;
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
			agentSenseComponent.ImpactEvent += OnImpactEvent;
		}

		protected void OnDisable()
		{
			agentSenseComponent.ImpactEvent -= OnImpactEvent;

			if (shakeExtension != null)
			{
				shakeExtension.Clear();
			}
		}

		protected void LateUpdate()
		{
			ApplyShake();
		}

		/// <summary>
		/// Apply a new screenshake source using provided parameters.
		/// </summary>
		public void Shake(Vector3 magnitude, Vector3 direction, float frequency, float duration, AnimationCurve falloff = null)
		{
			Shake(new ShakeSource(magnitude, direction, duration, frequency, falloff));
		}

		/// <summary>
		/// Apply a new screenshake source using provided <paramref name="settings"/>.
		/// </summary>
		public void Shake(ShakeSettings settings)
		{
			Shake(new ShakeSource(settings.Magnitude, Vector3.zero, settings.Duration, settings.Frequency, settings.Falloff));
		}

		/// <summary>
		/// Applies a new screenshake source.
		/// </summary>
		public void Shake(IShakeSource source)
		{
			sources.Add(source);
			//SpaxDebug.Log("SHAKE", $"magnitude={source.Magnitude}, time={source.Time}, completed={source.Completed}, intensity={source.EvaluateIntensity()}");
		}

		private void ApplyShake()
		{
			Vector3 angles = new Vector3(), magnitude, bias;
			Vector3 positionOffset = Vector3.zero;

			float intensity, noiseX, noiseY, x, y, z;
			for (int i = 0; i < sources.Count; i++)
			{
				// Remove and skip source if completed.
				if (sources[i] == null || sources[i].Completed)
				{
					sources[i]?.Dispose();
					sources.RemoveAt(i);
					i--;
					continue;
				}

				// Apply shake source.
				var source = sources[i];
				magnitude = source.Magnitude == Vector3.zero ? defaultSettings.Magnitude : source.Magnitude;
				intensity = source.EvaluateIntensity();
				noiseX = GenerateNoise(source.Seed, 0f, source.Time);
				noiseY = GenerateNoise(source.Seed, OFFSET, source.Time);
				bias = source.Direction == Vector3.zero ?
					magnitude :
					transform.InverseTransformDirection(source.Direction).normalized.Multiply(magnitude);

				// Rotation:
				x = bias.x + noiseX * magnitude.z;
				y = -bias.y + noiseY * magnitude.z;
				z = x * y * rollAmount;
				angles += new Vector3(y, x, z) * intensity;

				// Position:
				positionOffset += new Vector3(x, y, 0f) * intensity * positionMultiplier;
			}

			if (shakeExtension != null)
			{
				shakeExtension.SetRotationEuler(angles.Clamp(-maxAngle, maxAngle));
				shakeExtension.SetPositionLocal(positionOffset);
			}
		}

		/// <summary>
		/// Generates a 3 octave one dimensional noise from <paramref name="source"/>.
		/// </summary>
		private float GenerateNoise(int seed, float offset, float time)
		{
			Random.InitState(seed);
			float off = Random.value * OFFSET + offset;
			float a = Mathf.PerlinNoise1D(off + time).Clamp01().Remap(-1f, 1f) * SPECTRE;
			float b = Mathf.PerlinNoise1D(off * 2f + time * 2f).Clamp01().Remap(-1f, 1f) * SPECTRE * 0.5f;
			float c = Mathf.PerlinNoise1D(off * 4f + time * 4f).Clamp01().Remap(-1f, 1f) * SPECTRE * 0.25f;
			return a + b + c;
		}

		/// <summary>
		/// Converts impact data to a screenshake.
		/// </summary>
		private void OnImpactEvent(ImpactData impact)
		{
			if (impact.ShakeSource == null)
			{
				float multiplier = Mathf.Log10(impact.Force > 1f ? impact.Force : 1f); // 1=0, 10=1, 100=2
				Shake(defaultSettings.Magnitude * multiplier,
					impact.Direction,
					defaultSettings.Frequency * multiplier,
					defaultSettings.Duration * multiplier,
					defaultSettings.Falloff);
			}
			else
			{
				Shake(impact.ShakeSource);
			}
		}
	}
}
