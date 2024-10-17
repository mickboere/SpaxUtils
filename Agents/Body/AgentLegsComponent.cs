using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentLegsComponent : EntityComponentMono, ILegsComponent
	{
		public event Action<ILeg, bool> FootstepEvent;

		public IReadOnlyList<ILeg> Legs => legs;

		[SerializeField, Range(-1f, 1f)] private float mainCycleOffset;
		[SerializeField] private List<Leg> legs = new List<Leg>();

		private IGrounderComponent grounderComponent;

		public void InjectDependencies(IGrounderComponent grounderComponent)
		{
			this.grounderComponent = grounderComponent;
		}

		protected void OnEnable()
		{
			foreach (Leg leg in legs)
			{
				leg.FootstepEvent += OnFootstepEvent;
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

		protected void OnDisable()
		{
			foreach (Leg leg in legs)
			{
				leg.FootstepEvent -= OnFootstepEvent;
			}
		}

		private void OnFootstepEvent(ILeg leg, bool grounded)
		{
			FootstepEvent?.Invoke(leg, grounded);
		}
	}
}
