using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ProcBobbingAnimation : EntityComponentBase
	{
		[SerializeField] private float cycleOffset;
		[SerializeField] private float sensitivity = 0.5f;
		[SerializeField] private float maxHeight = 0.2f;
		[SerializeField] private AnimationCurve bobCurve;
		[SerializeField] private float smoothingSpeed = 8f;

		private RigidbodyWrapper wrapper;
		private LegWalkerComponent walker;
		private ILegsComponent legs;

		public void InjectDependencies(RigidbodyWrapper wrapper, LegWalkerComponent walker, ILegsComponent legs)
		{
			this.wrapper = wrapper;
			this.walker = walker;
			this.legs = legs;
		}

		protected void FixedUpdate()
		{
			float height = 0f;
			foreach (ILeg leg in legs.Legs)
			{
				height = Mathf.Max(height, bobCurve.Evaluate(walker.CalculateFootHeight(walker.GetProgress(leg.WalkCycleOffset + cycleOffset))));
			}

			//height -= 0.5f * walker.Effect;
			//height *= effectCurve.Evaluate(walker.Effect) * sensitivity * wrapper.Grip;

			transform.localPosition = Vector3.Lerp(transform.localPosition, Vector3.up * Mathf.Max(height, maxHeight), smoothingSpeed * Time.deltaTime);
		}
	}
}
