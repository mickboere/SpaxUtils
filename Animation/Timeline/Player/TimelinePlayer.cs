using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SpaxUtils
{
	/// <summary>
	/// A single <see cref="AnimationTimeline"/> playing in a <see cref="TimelineGraph"/> slot.
	/// The playhead is owned by whoever holds this handle; the clip never advances itself.
	/// </summary>
	public class TimelinePlayer
	{
		/// <summary>Raised for every marker start swept by a time change, in sweep order.</summary>
		public event Action<ResolvedMarker> MarkerCrossedEvent;

		public AnimationTimeline Timeline { get; }
		public float Time { get; private set; }

		/// <summary>Logical length, which can exceed the clip when a region sustains past its last frame.</summary>
		public float Duration => Timeline == null ? 0f : Timeline.Extent;

		/// <summary>
		/// Blend weight within the performance layer. Stored only; <see cref="TimelineGraph"/> folds every
		/// player's weight into the mixer and layer weights once per frame.
		/// </summary>
		public float Weight
		{
			get => weight;
			set => weight = Mathf.Clamp01(value);
		}

		internal int Slot { get; }
		internal AnimationClipPlayable Playable { get; }

		private readonly List<ResolvedMarker> crossings = new List<ResolvedMarker>();
		private float weight;

		internal TimelinePlayer(AnimationTimeline timeline, AnimationClipPlayable playable, int slot)
		{
			Timeline = timeline;
			Playable = playable;
			Slot = slot;
		}

		/// <summary>
		/// Moves the playhead to an absolute time, reporting every marker swept on the way.
		/// Jumps and reverse scrubs report correctly, which is the whole reason markers aren't AnimationEvents.
		/// </summary>
		public void SetTime(float time, bool reportCrossings = true)
		{
			float previous = Time;
			Time = Mathf.Clamp(time, 0f, Duration);

			// Timeline time may run past the clip during a sustain; the clip just holds its final frame.
			Playable.SetTime(Timeline == null ? Time : Mathf.Min(Time, Timeline.Duration));

			if (!reportCrossings || Timeline == null || MarkerCrossedEvent == null || Mathf.Approximately(previous, Time))
			{
				return;
			}

			Timeline.GetCrossings(previous, Time, crossings);
			foreach (ResolvedMarker marker in crossings)
			{
				MarkerCrossedEvent.Invoke(marker);
			}
		}

		/// <summary>Advances the playhead by <paramref name="delta"/> seconds.</summary>
		public void Advance(float delta)
		{
			SetTime(Time + delta);
		}

		/// <summary>Holds the playhead in place; the pose stays put for as long as it's parked.</summary>
		public void Park(string markerID)
		{
			SetTime(Timeline == null ? Time : Timeline.TimeOf(markerID, Time));
		}

		#region Timeline queries

		public bool IsInside(string id)
		{
			return Timeline != null && Timeline.IsInside(id, Time);
		}

		public bool TryGetMarker(string id, out ResolvedMarker marker)
		{
			if (Timeline == null)
			{
				marker = default;
				return false;
			}
			return Timeline.TryGetMarker(id, out marker);
		}

		/// <summary>Seconds from the current playhead to a marker; negative once it has passed.</summary>
		public float TimeUntil(string id, float fallback = 0f)
		{
			return TryGetMarker(id, out ResolvedMarker marker) ? marker.Start - Time : fallback;
		}

		#endregion Timeline queries

		public override string ToString()
		{
			return $"TimelinePlayer(\"{(Timeline == null ? "NULL" : Timeline.name)}\", t={Time:0.000}, w={weight:0.00})";
		}
	}
}
