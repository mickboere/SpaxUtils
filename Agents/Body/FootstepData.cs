using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public readonly struct FootstepData
	{
		public Leg Leg { get; }
		public bool Grounded { get; }
		public Dictionary<SurfaceConfiguration, float> Surfaces { get; }
		public Vector3 Position { get; }
		public Vector3 Normal { get; }
		public bool HasValidContact { get; }
		public bool IsLanding { get; }

		public FootstepData(
			Leg leg,
			bool grounded,
			Dictionary<SurfaceConfiguration, float> surfaces,
			Vector3 position,
			Vector3 normal,
			bool hasValidContact,
			bool isLanding)
		{
			Leg = leg;
			Grounded = grounded;
			Surfaces = surfaces;
			Position = position;
			Normal = normal;
			HasValidContact = hasValidContact;
			IsLanding = isLanding;
		}
	}
}
