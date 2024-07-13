using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(LineRenderer))]
	public class Vector8Visualizer : MonoBehaviour
	{
		[SerializeField] private Vector8 vector8;
		[SerializeField] private float scale = 1f;

		private RectTransform rectTransform;
		private LineRenderer lineRenderer;

		protected void Awake()
		{
			Initialize();
		}

		protected void OnValidate()
		{
			Initialize();
		}

		protected void Update()
		{
			if (!Application.isPlaying)
			{
				Visualize(vector8);
			}
		}

		public void Visualize(Vector8 vector8)
		{
			this.vector8 = vector8;

			Vector3[] positions = vector8.GetPositions3D();
			for (int i = 0; i < positions.Length; i++)
			{
				positions[i].x *= rectTransform.rect.size.x * 0.5f * scale;
				positions[i].y *= rectTransform.rect.size.y * 0.5f * scale;
			}
			lineRenderer.SetPositions(positions);
		}

		private void Initialize()
		{
			if (rectTransform == null)
			{
				rectTransform = GetComponent<RectTransform>();
			}
			if (lineRenderer == null)
			{
				lineRenderer = GetComponent<LineRenderer>();
			}

			lineRenderer.loop = true;
			lineRenderer.positionCount = 8;
		}
	}
}
