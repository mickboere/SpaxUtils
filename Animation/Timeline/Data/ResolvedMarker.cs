using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A <see cref="TimelineMarker"/> with its end resolved to an absolute time.
	/// Queries run against these so reference resolution is paid once, not per lookup.
	/// </summary>
	public readonly struct ResolvedMarker
	{
		public string ID { get; }
		public float Start { get; }
		public float End { get; }
		public AnimationCurve Curve { get; }
		public ILabeledDataProvider Data { get; }

		/// <summary>
		/// How the end was authored, which <see cref="IsRegion"/> cannot tell you: a zero-length Duration
		/// region resolves identically to a point, yet one states a length of zero and the other states nothing.
		/// </summary>
		public MarkerEnd EndMode { get; }

		/// <summary>Index of the authored marker this came from; identity survives sorting and duplicate IDs.</summary>
		public int Index { get; }

		public float Length => End - Start;
		public bool IsRegion => End > Start;

		public ResolvedMarker(string id, float start, float end, MarkerEnd endMode, AnimationCurve curve,
			ILabeledDataProvider data, int index = -1)
		{
			ID = id;
			Start = start;
			End = end;
			EndMode = endMode;
			Curve = curve;
			Data = data;
			Index = index;
		}

		/// <summary>
		/// Shapes a 0..1 progress through this marker's curve. Only an EMPTY curve is the identity; a single
		/// key is honoured as a constant, since holding a fixed value is a legitimate thing to author.
		/// </summary>
		public float Evaluate(float progress)
		{
			return Curve == null || Curve.length == 0 ? progress : Curve.Evaluate(progress);
		}

		public bool Contains(float time)
		{
			return time >= Start && time <= End;
		}

		/// <summary>
		/// Normalized progress through the region, 0 for points.
		/// Feed this to a <see cref="LabeledCurveData"/> in <see cref="Data"/> to evaluate across the region.
		/// </summary>
		public float Progress(float time)
		{
			return IsRegion ? Mathf.Clamp01((time - Start) / Length) : 0f;
		}

		public override string ToString()
		{
			return IsRegion ? $"ResolvedMarker(\"{ID}\", {Start}..{End})" : $"ResolvedMarker(\"{ID}\", {Start})";
		}
	}
}
