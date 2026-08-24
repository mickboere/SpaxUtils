using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// An <see cref="AnimationClip"/> paired with labeled markers and regions on its timeline.
	/// The clip is the atom of the animation pipeline; composition lives in blend assets above it.
	/// </summary>
	[CreateAssetMenu(fileName = "AnimationTimeline", menuName = "ScriptableObjects/Animation/AnimationTimeline")]
	public class AnimationTimeline : ScriptableObject
	{
		public AnimationClip Clip => clip;

		/// <summary>Length of the animation itself.</summary>
		public float Duration => clip != null ? clip.length : 0f;

		/// <summary>
		/// Furthest point the timeline reaches. Exceeds <see cref="Duration"/> when a marker sustains past the
		/// last animated frame; everything sampling the clip clamps, so the final pose simply holds.
		/// </summary>
		public float Extent
		{
			get
			{
				float extent = Duration;
				foreach (ResolvedMarker marker in Resolved)
				{
					if (marker.End > extent)
					{
						extent = marker.End;
					}
				}
				return extent;
			}
		}

		public IReadOnlyList<TimelineMarker> Markers => markers;
		public ILabeledDataProvider GlobalData => globalData;

		/// <summary>All markers with their ends resolved, ordered by start time.</summary>
		public IReadOnlyList<ResolvedMarker> Resolved
		{
			get
			{
				if (resolved == null)
				{
					Resolve();
				}
				return resolved;
			}
		}

		[SerializeField] private AnimationClip clip;
		[SerializeField, Tooltip("Data spanning the whole timeline, such as a locomotion cycle offset.")]
		private LabeledPoseData globalData;
		[SerializeField] private List<TimelineMarker> markers;

		private List<ResolvedMarker> resolved;

		/// <summary>
		/// Bulk setup for conversion tooling. Replaces clip and markers wholesale and drops the cache.
		/// </summary>
		public void Setup(AnimationClip clip, IEnumerable<TimelineMarker> markers, LabeledPoseData globalData = null)
		{
			this.clip = clip;
			this.markers = new List<TimelineMarker>(markers);
			if (globalData != null)
			{
				this.globalData = globalData;
			}
			resolved = null;
		}

		#region Resolution

		/// <summary>
		/// Rebuilds the resolved marker cache. Called lazily; only needed directly after runtime mutation.
		/// </summary>
		public void Resolve()
		{
			Build(null);
		}

		/// <summary>
		/// Rebuilds the cache and reports authoring problems instead of logging them. Half-configured markers
		/// are a normal editing state, so this surfaces in the inspector rather than the console.
		/// </summary>
		public void Validate(List<string> problems)
		{
			problems.Clear();
			Build(problems);
		}

		private void Build(List<string> problems)
		{
			resolved = new List<ResolvedMarker>(markers == null ? 0 : markers.Count);
			if (markers == null)
			{
				return;
			}

			for (int i = 0; i < markers.Count; i++)
			{
				TimelineMarker marker = markers[i];
				if (marker == null)
				{
					continue;
				}

				if (string.IsNullOrEmpty(marker.ID))
				{
					problems?.Add("A marker has no identifier and is ignored.");
					continue;
				}

				// Nothing is checked against the clip's length. A marker may START past the last frame, not
				// just end past it - that is how a move sustains its final pose for a longer performance.
				float end = marker.Time;
				switch (marker.EndMode)
				{
					case MarkerEnd.Duration:
						end = marker.Time + Mathf.Max(0f, marker.Duration);
						break;
					case MarkerEnd.Marker:
						end = ResolveEndMarker(marker, problems);
						break;
				}

				resolved.Add(new ResolvedMarker(marker.ID, marker.Time, end, marker.EndMode, marker.Curve, marker.Data, i));
			}

			resolved.Sort((a, b) => a.Start.CompareTo(b.Start));

			if (problems != null)
			{
				ValidatePhaseOffsets(problems);
			}
		}

		/// <summary>
		/// Offsets are measured from the Performing origin and clamp at zero, so a marker placed before it
		/// silently reads as no delay at all rather than as the position it was authored at.
		/// </summary>
		private void ValidatePhaseOffsets(List<string> problems)
		{
			float origin = 0f;
			bool found = false;
			foreach (ResolvedMarker marker in resolved)
			{
				if (marker.ID == TimelineMarkerIdentifiers.PERFORMING)
				{
					origin = marker.Start;
					found = true;
					break;
				}
			}

			if (!found)
			{
				return;
			}

			foreach (ResolvedMarker marker in resolved)
			{
				if ((marker.ID != TimelineMarkerIdentifiers.HIT && marker.ID != TimelineMarkerIdentifiers.INERTIA) ||
					marker.Start >= origin)
				{
					continue;
				}

				problems.Add($"'{marker.ID.LastDivision()}' starts at {marker.Start:0.000}s, before Performing " +
					$"({origin:0.000}s). Its delay clamps to 0 and it fires the instant the swing begins.");
			}
		}

		/// <summary>
		/// Binds to the START of the next marker carrying the referenced identifier, at or after this one.
		/// Starts are authored rather than derived, so this is a single hop and can never form a cycle -
		/// which is why the target may itself be a region.
		/// </summary>
		private float ResolveEndMarker(TimelineMarker marker, List<string> problems)
		{
			if (string.IsNullOrEmpty(marker.EndMarker))
			{
				problems?.Add($"'{marker.ID}' has no end marker assigned.");
				return marker.Time;
			}

			float best = float.MaxValue;
			foreach (TimelineMarker candidate in markers)
			{
				// Never bind to itself - the dropdown offers a marker's own identifier, and a marker
				// cannot terminate itself. A *different* marker sharing the identifier is still valid.
				if (candidate == null || ReferenceEquals(candidate, marker) ||
					candidate.ID != marker.EndMarker || candidate.Time < marker.Time)
				{
					continue;
				}

				if (candidate.Time < best)
				{
					best = candidate.Time;
				}
			}

			if (best == float.MaxValue)
			{
				problems?.Add($"'{marker.ID}' found no later marker named '{marker.EndMarker}'. Collapsed to a point.");
				return marker.Time;
			}

			return best;
		}

		#endregion Resolution

		#region Queries

		/// <summary>
		/// Looks a resolved marker up by its authored index. Identity, unlike ID plus time, stays unique when
		/// two markers share both.
		/// </summary>
		public bool TryGetResolved(int markerIndex, out ResolvedMarker marker)
		{
			foreach (ResolvedMarker candidate in Resolved)
			{
				if (candidate.Index == markerIndex)
				{
					marker = candidate;
					return true;
				}
			}

			marker = default;
			return false;
		}

		/// <summary>
		/// Returns the first marker carrying <paramref name="id"/>. This is the lookahead AI reads from.
		/// </summary>
		public bool TryGetMarker(string id, out ResolvedMarker marker)
		{
			foreach (ResolvedMarker candidate in Resolved)
			{
				if (candidate.ID == id)
				{
					marker = candidate;
					return true;
				}
			}

			marker = default;
			return false;
		}

		/// <summary>All markers carrying <paramref name="id"/>, in time order.</summary>
		public IEnumerable<ResolvedMarker> GetMarkers(string id)
		{
			foreach (ResolvedMarker candidate in Resolved)
			{
				if (candidate.ID == id)
				{
					yield return candidate;
				}
			}
		}

		/// <summary>Start time of the first marker carrying <paramref name="id"/>.</summary>
		public float TimeOf(string id, float fallback = 0f)
		{
			return TryGetMarker(id, out ResolvedMarker marker) ? marker.Start : fallback;
		}

		/// <summary>Whether <paramref name="time"/> falls inside any region carrying <paramref name="id"/>.</summary>
		public bool IsInside(string id, float time)
		{
			foreach (ResolvedMarker candidate in Resolved)
			{
				if (candidate.ID == id && candidate.Contains(time))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Collects every marker start swept between two playhead positions, in sweep order. Being a span
		/// query it survives jumps and reverse scrubbing, which is exactly where AnimationEvents fail.
		/// </summary>
		public void GetCrossings(float from, float to, List<ResolvedMarker> results)
		{
			results.Clear();

			bool forward = to >= from;
			float min = forward ? from : to;
			float max = forward ? to : from;

			foreach (ResolvedMarker marker in Resolved)
			{
				// Exclusive lower bound: consecutive frames must not report the same marker twice.
				if (marker.Start > min && marker.Start <= max)
				{
					results.Add(marker);
				}
			}

			if (!forward)
			{
				results.Reverse();
			}
		}

		#endregion Queries

		// Domain reload is disabled in this project, so the cache must be dropped on BOTH hooks
		// or it silently latches stale values across play sessions and asset edits.
		protected void OnEnable()
		{
			resolved = null;
		}

		protected void OnValidate()
		{
			resolved = null;
		}
	}
}
