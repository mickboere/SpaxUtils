using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentLegsComponent : EntityComponentBase, ILegsComponent
	{
		public event Action<ILeg, bool> FootstepEvent;

		public IReadOnlyList<ILeg> Legs => legs;

		[SerializeField, Range(-1f, 1f)] private float mainCycleOffset;
		[SerializeField] private List<Leg> legs = new List<Leg>();

		protected void OnEnable()
		{
			foreach (Leg leg in legs)
			{
				leg.FootstepEvent += OnFootstepEvent;
			}
		}

		protected void Update()
		{
			foreach (Leg leg in legs)
			{
				leg.MainCycleOffset = mainCycleOffset;
			}
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
