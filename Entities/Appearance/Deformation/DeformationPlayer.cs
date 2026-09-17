using System.Collections.Generic;
using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// Plays ripples and holds smears on one body, writing them to an <see cref="IAppearanceEffects"/>.
	/// Shared by <see cref="EntityDeformer"/> and the editor preview.
	/// </summary>
	public class DeformationPlayer
	{
		private class SmearState
		{
			public readonly object requestId = new object();
			public SmearProfile profile;
			public float intensity;
			public float startTime;

			// Agent-local, so the smear stays on the body when it is knocked away.
			public Vector3 localOrigin;
			public Vector3 localDirection;

			public Transform follow;
			public Vector3 lastFollowPosition;
			public Vector3 followDirection;
			public bool followSampled;
		}

		/// <summary>
		/// Seconds accumulated through <see cref="Update"/>.
		/// </summary>
		public float Time { get; private set; }

		public bool IsActive => HasRipple || smears.Count > 0;

		public bool HasRipple => rippleProfile != null;

		private readonly Transform body;
		private readonly ITargetable targetable;
		private readonly IAppearanceEffects output;

		private readonly object rippleRequestId = new object();
		private RippleProfile rippleProfile;
		private Vector3 rippleLocalPoint;
		private Vector3 rippleLocalDirection;
		private float rippleIntensity;
		private float rippleStartTime;

		private readonly Dictionary<object, SmearState> smears = new Dictionary<object, SmearState>();

		public DeformationPlayer(Transform body, ITargetable targetable, IAppearanceEffects output)
		{
			this.body = body;
			this.targetable = targetable;
			this.output = output;
		}

		/// <summary>
		/// Starts a ripple pushing along <paramref name="direction"/> from <paramref name="point"/>, replacing any running one.
		/// </summary>
		public void PlayRipple(RippleProfile profile, Vector3 point, Vector3 direction, float intensity)
		{
			if (profile == null)
			{
				return;
			}

			rippleProfile = profile;
			rippleLocalPoint = body.InverseTransformPoint(point);
			rippleLocalDirection = body.InverseTransformDirection(direction.normalized);
			rippleIntensity = intensity;
			rippleStartTime = Time;
		}

		public void StopRipple()
		{
			rippleProfile = null;
			output.Clear(rippleRequestId);
		}

		/// <summary>
		/// Holds a smear trailing along <paramref name="direction"/> until <see cref="ClearSmear"/>; call again to move it.
		/// </summary>
		public void SetSmear(object id, SmearProfile profile, Vector3 origin, Vector3 direction, float intensity)
		{
			SmearState state = GetSmear(id, profile);
			if (state == null)
			{
				return;
			}

			state.follow = null;
			state.localOrigin = body.InverseTransformPoint(origin);
			state.localDirection = body.InverseTransformDirection(direction.normalized);
			state.intensity = intensity;
		}

		/// <summary>
		/// Holds a smear at <paramref name="follow"/> (e.g. a fist bone), trailing its motion.
		/// </summary>
		public void SetSmear(object id, SmearProfile profile, Transform follow, float intensity)
		{
			SmearState state = GetSmear(id, profile);
			if (state == null || follow == null)
			{
				return;
			}

			if (state.follow != follow)
			{
				state.follow = follow;
				state.followSampled = false;
				state.followDirection = Vector3.zero;
			}
			state.intensity = intensity;
		}

		public void SetSmearIntensity(object id, float intensity)
		{
			if (id != null && smears.TryGetValue(id, out SmearState state))
			{
				state.intensity = intensity;
			}
		}

		public void ClearSmear(object id)
		{
			if (id != null && smears.TryGetValue(id, out SmearState state))
			{
				output.Clear(state.requestId);
				smears.Remove(id);
			}
		}

		public void ClearAll()
		{
			StopRipple();
			foreach (SmearState state in smears.Values)
			{
				output.Clear(state.requestId);
			}
			smears.Clear();
		}

		public void Update(float deltaTime)
		{
			Time += deltaTime;

			if (!IsActive)
			{
				return;
			}

			DeformBody.GetBody(targetable, body, out float floor, out float height, out float size);

			// Smears first: their floor drop lowers the shared pin, so the ripple reaches the feet too.
			foreach (SmearState state in smears.Values)
			{
				UpdateSmear(state, floor, height);
			}

			UpdateRipple(floor, height, size);
		}

		private SmearState GetSmear(object id, SmearProfile profile)
		{
			if (id == null || profile == null)
			{
				return null;
			}

			if (!smears.TryGetValue(id, out SmearState state))
			{
				state = new SmearState { startTime = Time };
				smears.Add(id, state);
			}

			state.profile = profile;
			return state;
		}

		private void UpdateRipple(float floor, float height, float size)
		{
			if (rippleProfile == null)
			{
				return;
			}

			Vector3 point = body.TransformPoint(rippleLocalPoint);
			Vector3 direction = body.TransformDirection(rippleLocalDirection);

			if (!rippleProfile.Evaluate(point, direction, rippleIntensity, Time - rippleStartTime, size,
				out DeformRipple ripple))
			{
				StopRipple();
				return;
			}

			output.RequestRipple(rippleRequestId, ripple, floor, height);
		}

		private void UpdateSmear(SmearState state, float floor, float height)
		{
			Vector3 origin;
			Vector3 direction;

			if (state.follow != null)
			{
				origin = state.follow.position;
				SampleFollow(state);
				direction = -state.followDirection;
			}
			else
			{
				origin = body.TransformPoint(state.localOrigin);
				direction = body.TransformDirection(state.localDirection);
			}

			DeformSmear smear = state.profile.Evaluate(origin, direction, state.intensity, Time - state.startTime,
				out float floorDrop, out float bodyFade);

			output.RequestSmear(state.requestId, smear, floor - floorDrop, height + floorDrop);
			output.RequestFade(state.requestId, 0, 1f, bodyFade);
		}

		/// <summary>
		/// Motion direction of the followed transform; holds the last one while it stands still.
		/// </summary>
		private static void SampleFollow(SmearState state)
		{
			Vector3 position = state.follow.position;
			Vector3 moved = position - state.lastFollowPosition;

			if (state.followSampled && moved.sqrMagnitude > 0.0000001f)
			{
				state.followDirection = moved.normalized;
			}

			state.lastFollowPosition = position;
			state.followSampled = true;
		}
	}
}
