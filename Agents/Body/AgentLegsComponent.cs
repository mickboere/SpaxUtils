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

		private GrounderComponent grounderComponent;
		private SurfaceLibrary surfaceLibrary;
		private EntityStat elevationStat;
		private RuntimeDataEntry feetSurfaceData;

		public void InjectDependencies(GrounderComponent grounderComponent, SurfaceLibrary surfaceLibrary)
		{
			this.grounderComponent = grounderComponent;
			this.surfaceLibrary = surfaceLibrary;
			elevationStat = Entity.Stats.GetStat(AgentDataIdentifiers.ELEVATION, true, 0f);
			feetSurfaceData = Entity.RuntimeData.GetEntry(AgentDataIdentifiers.FEET_SURFACE,
				new RuntimeDataEntry(AgentDataIdentifiers.FEET_SURFACE, "", false));
		}

		protected void OnEnable()
		{
			foreach (Leg leg in legs)
			{
				leg.Initialize(surfaceLibrary);
				leg.FootstepEvent += OnFootstepEvent;
			}
			elevationStat.ValueChangedEvent += OnElevationChange;
			feetSurfaceData.ValueChangedEvent += OnFeetSurfaceChange;
			OnElevationChange();
		}

		protected void OnDisable()
		{
			foreach (Leg leg in legs)
			{
				leg.FootstepEvent -= OnFootstepEvent;
			}
			elevationStat.ValueChangedEvent -= OnElevationChange;
			feetSurfaceData.ValueChangedEvent -= OnFeetSurfaceChange;
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
				//leg.Update();
			}
		}

		protected void FixedUpdate()
		{
			foreach (Leg leg in legs)
			{
				leg.Update();
			}
		}

		private void OnFootstepEvent(Leg leg, bool grounded, Dictionary<SurfaceConfiguration, float> surfaces)
		{
			FootstepEvent?.Invoke(leg, grounded, surfaces);
		}

		private void OnElevationChange()
		{
			foreach (Leg leg in legs)
			{
				leg.Elevation = elevationStat.Value;
			}
		}

		private void OnFeetSurfaceChange(object value)
		{
			foreach (Leg leg in legs)
			{
				leg.FootSurface = (string)value;
			}
		}
	}
}
