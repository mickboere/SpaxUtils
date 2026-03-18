using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// (Humanoid) Leg configuration data.
	/// </summary>
	[Serializable]
	public class Leg
	{
		/// <summary>
		/// Invoked when the leg's grounded state is updated.
		/// </summary>
		public event Action<FootstepData> FootstepEvent;

		/// <summary>
		/// Whether the leg is currently grounded by normal surveyor logic.
		/// </summary>
		public bool Grounded { get; private set; }

		/// <summary>
		/// Whether the leg is currently considered in contact for visual/audio FX purposes.
		/// This includes normal grounding and IK clipping rescue.
		/// </summary>
		public bool FXGrounded => Grounded || Clipping;

		/// <summary>
		/// Whether the IK leg is currently being rescued by clipping contact.
		/// This must not be treated as a true gait foot plant.
		/// </summary>
		public bool Clipping { get; set; }

		/// <summary>
		/// The grounded amount, 1 being grounded, 0 being lifted.
		/// </summary>
		public float GroundedAmount { get; private set; }

		/// <summary>
		/// Whether the current grounded point is valid (invalid means too great an angle or other factors).
		/// </summary>
		public bool ValidGround { get; private set; }

		/// <summary>
		/// The target grounded point of the leg.
		/// </summary>
		public Vector3 TargetPoint { get; private set; }

		/// <summary>
		/// The last <see cref="RaycastHit"/> for this leg.
		/// </summary>
		public RaycastHit GroundedHit { get; private set; }

		/// <summary>
		/// The raycast origin for the grounded check.
		/// </summary>
		public Vector3 CastOrigin { get; set; }

		/// <summary>
		/// The raycast direction for the grounded check.
		/// </summary>
		public Vector3 CastDirection { get; set; }

		/// <summary>
		/// The (walking) cycle offset for this specific leg.
		/// </summary>
		public float WalkCycleOffset { get => walkCycleOffset + MainCycleOffset; }

		/// <summary>
		/// The global (walking) cycle offset.
		/// </summary>
		public float MainCycleOffset { get; set; }

		/// <summary>
		/// Overridable elevation value, if set to 0 will return default elevation value.
		/// </summary>
		public float Elevation { get { return elevationOverride.Approx(0f) ? elevation : elevationOverride; } set { elevationOverride = value; } }

		/// <summary>
		/// Overridable foot-surface value, if set to null/empty will return default elevation value.
		/// </summary>
		public string FootSurface { get { return surfaceOverride.IsNullOrEmpty() ? footSurface : surfaceOverride; } set { surfaceOverride = value; } }

		/// <summary>
		/// All data concerning the surface this leg last stepped on.
		/// </summary>
		public Dictionary<SurfaceConfiguration, float> SurfaceData { get; private set; }

		/// <summary>
		/// The total length of this leg.
		/// </summary>
		public float Length => (KneePos - ThighPos).magnitude + (FootPos - KneePos).magnitude;

		[field: SerializeField] public Transform Thigh { get; private set; }
		public Vector3 ThighPos { get; private set; }
		[field: SerializeField] public Transform Knee { get; private set; }
		public Vector3 KneePos { get; private set; }
		[field: SerializeField] public Transform Foot { get; private set; }
		public Vector3 FootPos { get; private set; }

		/// <summary>
		/// Helper transform placed at the bottom of the foot, facing towards the toes.
		/// </summary>
		[field: SerializeField] public Transform Sole { get; private set; }
		public Vector3 SolePos { get; private set; }

		[SerializeField, Range(-1f, 1f)] private float walkCycleOffset;
		[SerializeField] private float elevation;
		[SerializeField, ConstDropdown(typeof(ISurfaceTypeConstants), true)] private string footSurface;

		private SurfaceLibrary surfaceLibrary;

		private float elevationOverride;
		private string surfaceOverride;
		private bool wasGrounded;

		public void Initialize(SurfaceLibrary surfaceLibrary)
		{
			this.surfaceLibrary = surfaceLibrary;
			SurfaceData = new Dictionary<SurfaceConfiguration, float>();
		}

		public void Update()
		{
			if ((ValidGround || !Grounded) && wasGrounded != Grounded)
			{
				// Foot was either grounded or lifted.
				wasGrounded = Grounded;
				if (Grounded)
				{
					// Collect surface data.
					SurfaceData.Clear();
					surfaceLibrary.BuildSurfaceData(GroundedHit, SurfaceData);
				}

				// Invoke footstep event with immutable contact data.
				FootstepEvent?.Invoke(CreateFootstepData(false));
			}
		}

		/// <summary>
		/// Update the foot physics data.
		/// </summary>
		public void UpdateGround(bool grounded, float groundedAmount, bool validGround, Vector3 targetPoint, RaycastHit groundedHit)
		{
			Grounded = grounded;
			GroundedAmount = groundedAmount;
			ValidGround = validGround;
			TargetPoint = targetPoint;
			GroundedHit = groundedHit;
		}

		/// <summary>
		/// Called in LateUpdate, keeps track of animated leg bone positions so that FixedUpdate can safely and accurately retrieve them.
		/// </summary>
		public void UpdatePositions()
		{
			ThighPos = Thigh.position;
			KneePos = Knee.position;
			FootPos = Foot.position;
			SolePos = Sole.position;
		}

		public void InvokeLanding(Dictionary<SurfaceConfiguration, float> surfaceData, Vector3 position, Vector3 normal)
		{
			Grounded = true;
			wasGrounded = true;

			SurfaceData.Clear();
			if (surfaceData != null)
			{
				foreach (KeyValuePair<SurfaceConfiguration, float> pair in surfaceData)
				{
					SurfaceData[pair.Key] = pair.Value;
				}
			}

			FootstepEvent?.Invoke(CreateFootstepData(true, position, normal, true));
		}

		private FootstepData CreateFootstepData(bool isLanding, Vector3? overridePosition = null, Vector3? overrideNormal = null, bool forceValidContact = false)
		{
			Vector3 position = overridePosition ?? GetBestContactPosition();
			Vector3 normal = overrideNormal ?? GetBestContactNormal();
			bool hasValidContact = forceValidContact || HasValidContact(position);

			return new FootstepData(this, Grounded, SurfaceData, position, normal, hasValidContact, isLanding);
		}

		private Vector3 GetBestContactPosition()
		{
			if (ValidGround && GroundedHit.collider != null)
			{
				return GroundedHit.point;
			}

			if (TargetPoint != Vector3.zero)
			{
				return TargetPoint;
			}

			if (Sole != null)
			{
				return SolePos;
			}

			return Foot != null ? FootPos : Vector3.zero;
		}

		private Vector3 GetBestContactNormal()
		{
			if (ValidGround && GroundedHit.collider != null && GroundedHit.normal != Vector3.zero)
			{
				return GroundedHit.normal;
			}

			if (Sole != null)
			{
				return Sole.up;
			}

			if (Foot != null)
			{
				return Foot.up;
			}

			return Vector3.up;
		}

		private bool HasValidContact(Vector3 position)
		{
			return position != Vector3.zero || Sole != null || Foot != null;
		}
	}
}
