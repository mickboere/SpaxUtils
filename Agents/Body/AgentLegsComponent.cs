using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentLegsComponent : EntityComponentMono
	{
		public event Action<Leg, bool, Dictionary<SurfaceConfiguration, float>> FootstepEvent;

		public IReadOnlyList<Leg> Legs => legs;

		[SerializeField, Range(-1f, 1f)] private float mainCycleOffset;
		[SerializeField] private List<Leg> legs = new List<Leg>();

		private IGrounderComponent grounderComponent;
		private SurfaceLibrary surfaceLibrary;

		public void InjectDependencies(IGrounderComponent grounderComponent, SurfaceLibrary surfaceLibrary)
		{
			this.grounderComponent = grounderComponent;
			this.surfaceLibrary = surfaceLibrary;
		}

		protected void OnEnable()
		{
			foreach (Leg leg in legs)
			{
				leg.Initialize(surfaceLibrary);
				leg.FootstepEvent += OnFootstepEvent;
			}
		}

		protected void OnDisable()
		{
			foreach (Leg leg in legs)
			{
				leg.FootstepEvent -= OnFootstepEvent;
			}
		}

		protected void Update()
		{
			float elevation = 0f;
			foreach (Leg leg in legs)
			{
				leg.MainCycleOffset = mainCycleOffset;
				elevation += leg.Elevation;
			}
			elevation /= legs.Count;
			grounderComponent.Elevation = elevation;
		}

		protected void LateUpdate()
		{
			foreach (Leg leg in legs)
			{
				leg.UpdatePositions();
			}
		}

		private void OnFootstepEvent(Leg leg, bool grounded, Dictionary<SurfaceConfiguration, float> surfaces)
		{
			FootstepEvent?.Invoke(leg, grounded, surfaces);
		}
	}
}
