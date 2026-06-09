using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Shared "orbit a foe" steering primitive for the circling combat behaviours (Hostile, Pressure).
	/// Owns the lateral strafe <see cref="PerlinHelper"/> and the two identical foe-facing steer moves
	/// (back off / close in). Each behaviour keeps its own zone-trigger logic and any bespoke hold/strafe
	/// drift; this only removes the duplicated perlin lifecycle and steer calls.
	/// </summary>
	public class OrbitStrafer
	{
		private readonly PerlinHelper lateral;

		/// <summary>Advances and returns the lateral strafe value (−1…1) by <paramref name="delta"/>.</summary>
		public float Lateral(float delta) => lateral.Update(delta);

		/// <summary>The lateral perlin's current time, for seeding correlated bespoke noise (read-only, does not advance).</summary>
		public float LateralTime => lateral.Time;

		public OrbitStrafer(PerlinHelperSettings settings, float polarization, float frequency)
		{
			lateral = new PerlinHelper(settings, polarization, frequency, -1f, 1f);
		}

		public void Dispose()
		{
			lateral.Dispose();
		}

		/// <summary>Back straight away from the foe while strafing laterally.</summary>
		public void SteerBackOff(AgentNavigationHandler navigation, Vector3 lookDirection, IWorldRegion region,
			IReadOnlyList<ITargetable> separation, float input, float delta)
		{
			Vector3 localInput = (Vector3.back + Vector3.right * lateral.Update(delta)).normalized * input;
			navigation.TrySteerLocal(localInput, lookDirection, region, 2f, separation, out _);
		}

		/// <summary>Close in on the foe (forward × <paramref name="speed"/>) while strafing laterally.</summary>
		public void SteerCloseIn(AgentNavigationHandler navigation, Vector3 lookDirection, IWorldRegion region,
			IReadOnlyList<ITargetable> separation, float speed, float input, float delta)
		{
			Vector3 localInput = Vector3.forward * speed + Vector3.right * lateral.Update(delta) * input;
			navigation.TrySteerLocal(localInput, lookDirection, region, 2f, separation, out _);
		}
	}
}
