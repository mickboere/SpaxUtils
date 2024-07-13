using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(Vector8Visualizer))]
	public class AEMOITest : MonoBehaviour
	{
		[SerializeField] private AEMOISettings settings;
		[SerializeField] private ElementOcton personality;
		[SerializeField] private Vector8 stimulation;

		private AEMOI aemoi;
		private Vector8Visualizer visualizer;

		protected void Awake()
		{
			aemoi = new AEMOI(settings, personality);
			visualizer = GetComponent<Vector8Visualizer>();
		}

		protected void OnEnable()
		{
			aemoi.Activate(true);
		}

		protected void OnDisable()
		{
			aemoi.Deactivate();
		}

		protected void Update()
		{
			aemoi.Stimulate(stimulation);
			aemoi.Update(Time.deltaTime);
			visualizer.Visualize(aemoi.Emotion);
		}
	}
}
