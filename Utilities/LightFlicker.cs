using SpaxUtils;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[ExecuteAlways, RequireComponent(typeof(Light))]
	public class LightFlicker : MonoBehaviour
	{
		public float Multiplier { get; set; } = 1f;

		[SerializeField] private Vector2 intensity = new Vector2(0.5f, 1f);
		[SerializeField] private Vector2 range = new Vector2(5f, 10f);
		[SerializeField] private PerlinHelper noiseA;
		[SerializeField] private PerlinHelper noiseB;
		[SerializeField] private bool executeInEditMode;
#if UNITY_EDITOR
		[SerializeField, ReadOnly] private Color a;
		[SerializeField, ReadOnly] private Color b;
		[SerializeField, ReadOnly] private Color x;
#endif

		private Light _light;

		protected void OnEnable()
		{
			Initialize();
		}

		protected void Update()
		{
			if (!executeInEditMode && !Application.isPlaying)
			{
				return;
			}

			float a = noiseA.Update(Time.deltaTime);
			float b = noiseB.Update(Time.deltaTime);
			float x = a.Max(b);
#if UNITY_EDITOR
			this.a = new Color(a, a, a, 1f);
			this.b = new Color(b, b, b, 1f);
			this.x = new Color(x, x, x, 1f);
#endif
			_light.intensity = intensity.Lerp(x) * Multiplier;
			_light.range = range.Lerp(x) * Multiplier;
		}

		protected void OnDrawGizmosSelected()
		{
			if (Selection.activeGameObject != gameObject)
			{
				return;
			}

			Gizmos.color = new Color(intensity.x, intensity.x, intensity.x, 1f);
			Gizmos.DrawWireSphere(transform.position, range.x * Multiplier);
			Gizmos.color = new Color(intensity.y, intensity.y, intensity.y, 1f);
			Gizmos.DrawWireSphere(transform.position, range.y * Multiplier);

#if UNITY_EDITOR
			// Ensure continuous Update calls.
			if (executeInEditMode && !Application.isPlaying)
			{
				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
				UnityEditor.SceneView.RepaintAll();
			}
#endif
		}

		private void Initialize()
		{
			_light = GetComponent<Light>();
			Multiplier = 1f;
			noiseA?.Initialize();
			noiseB?.Initialize();
		}
	}
}
