using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A labeled point or region authored on an <see cref="AnimationTimeline"/>.
	/// Ends are paired by identity, never by proximity, so inserting a marker never re-pairs a region.
	/// </summary>
	[Serializable]
	public class TimelineMarker
	{
		public string ID => id;
		public float Time => time;
		public MarkerEnd EndMode => endMode;
		public float Duration => duration;
		public string EndMarker => endMarker;
		public AnimationCurve Curve => curve;
		public ILabeledDataProvider Data => data;

		[SerializeField, ConstDropdown(typeof(ITimelineMarkerIdentifiers))] private string id;
		[SerializeField, Tooltip("Position on the clip, in seconds.")] private float time;
		[SerializeField] private MarkerEnd endMode;
		[SerializeField, Conditional(nameof(endMode), (int)MarkerEnd.Duration)] private float duration;
		[SerializeField, Conditional(nameof(endMode), (int)MarkerEnd.Marker),
			ConstDropdown(typeof(ITimelineMarkerIdentifiers)),
			Tooltip("Ends where the next marker with this identifier starts, so re-timing keeps the region coherent.")]
		private string endMarker;
		[SerializeField, Tooltip("Shapes progress through this marker. Left empty it has no effect.")]
		private AnimationCurve curve = new AnimationCurve();
		[SerializeField, Tooltip("Extra labeled data for systems that need more than a curve.")]
		private LabeledPoseData data;

		public TimelineMarker(string id, float time)
		{
			this.id = id;
			this.time = time;
			endMode = MarkerEnd.Point;
		}

		public TimelineMarker(string id, float time, float duration) : this(id, time)
		{
			endMode = MarkerEnd.Duration;
			this.duration = Mathf.Max(0f, duration);
		}

		public TimelineMarker(string id, float time, string endMarker) : this(id, time)
		{
			endMode = MarkerEnd.Marker;
			this.endMarker = endMarker;
		}

		/// <summary>Editor-only reposition; runtime treats markers as authored data.</summary>
		public void SetTime(float value)
		{
			time = value;
		}

		/// <summary>Editor-only resize of a <see cref="MarkerEnd.Duration"/> region.</summary>
		public void SetDuration(float value)
		{
			duration = Mathf.Max(0f, value);
		}

		/// <summary>Editor-only; conversion tooling carries authored curves across.</summary>
		public void SetCurve(AnimationCurve value)
		{
			curve = value;
		}

		public override string ToString()
		{
			return $"TimelineMarker(\"{id}\", {time}, {endMode})";
		}
	}
}
