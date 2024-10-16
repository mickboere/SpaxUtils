using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[ExecuteInEditMode]
	public class GradientFog : MonoBehaviour
	{
		[SerializeField] private Gradient gradient = new Gradient();
		[SerializeField] private Color originalColor;

		protected void Awake()
		{
			originalColor = RenderSettings.fogColor;
		}

		protected void OnDestroy()
		{
			RenderSettings.fogColor = originalColor;
		}

		protected void OnRenderObject()
		{
			Camera camera;
#if UNITY_EDITOR
			if (Application.isPlaying)
			{
				camera = Camera.main;
			}
			else
			{
				camera = SceneView.GetAllSceneCameras().FirstOrDefault();
			}
#else
			camera = Camera.main;
#endif

			float evaluation = 0.5f;
			if (camera != null)
			{
				evaluation = camera.transform.forward.NormalizedDot(Vector3.up);
			}

			RenderSettings.fogColor = gradient.Evaluate(evaluation);
		}
	}
}
