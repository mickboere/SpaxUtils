using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// How a move's <see cref="AgentTrailEffect"/> trail looks; owned by the behaviour that starts it.
	/// </summary>
	[Serializable]
	public class TrailSettings
	{
		[field: SerializeField] public Material Material { get; private set; }

		[field: SerializeField, Tooltip("Meters travelled between snapshots.")]
		public float Spacing { get; private set; } = 0.4f;

		[field: SerializeField, Tooltip("Seconds a snapshot lives while it fades out.")]
		public float Duration { get; private set; } = 0.4f;

		[field: SerializeField, Tooltip("How snapshots carry the body's smear: frozen at capture or still scrolling.")]
		public TrailSmearMode Smear { get; private set; } = TrailSmearMode.Frozen;

		[field: SerializeField, Tooltip("Stretch each snapshot's smear back to the previous snapshot, ignoring falloff.")]
		public bool MatchSpacing { get; private set; } = true;

		[field: SerializeField, Tooltip("Multiplier on the matched smear length, to close gaps the streaks and pin leave.")]
		public float Reach { get; private set; } = 1f;
	}
}
