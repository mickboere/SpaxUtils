using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Pure advancement of a <see cref="PerformanceSample"/>. Split into phases so a caller can interleave
	/// events at the exact points the original inline clock did, while an editor preview uses
	/// <see cref="Advance"/> to run the whole thing.
	/// </summary>
	public static class PerformanceClock
	{
		/// <summary>
		/// Advances a full frame. Equivalent to the phase calls in order; use those when an event has to
		/// fire between charging and performing.
		/// </summary>
		public static PerformanceSample Advance(IPerformanceMove move, PerformanceSample sample,
			PerformanceClockInput input, bool canceled)
		{
			if (canceled)
			{
				return AdvanceCancel(move, sample, input);
			}

			sample = AdvanceCharge(move, sample, input);
			return AdvancePerformance(move, sample, input);
		}

		/// <summary>
		/// Charging. Clamps to <see cref="IPerformanceMove.MinCharge"/> on the frame a held input is released
		/// so a late release cannot overshoot into an overcharge it never asked for.
		/// </summary>
		public static PerformanceSample AdvanceCharge(IPerformanceMove move, PerformanceSample sample,
			PerformanceClockInput input)
		{
			if (sample.State != PerformanceState.Preparing)
			{
				return sample;
			}

			if (sample.ChargeTime < move.MinCharge && input.Released && sample.ChargeTime + input.Delta >= move.MinCharge)
			{
				sample.ChargeTime = move.MinCharge;
				sample.State = PerformanceState.Performing;
			}
			else
			{
				sample.ChargeTime += input.Delta;
				if (sample.ChargeTime >= move.MinCharge && input.Released)
				{
					sample.State = PerformanceState.Performing;
				}
			}

			sample.Weight = Mathf.Clamp01(sample.ChargeTime / move.MinCharge);
			return sample;
		}

		/// <summary>
		/// Performing. Must run in the SAME frame charging completes - the original had no else between the
		/// two branches specifically to avoid a frame of delay, and callers must preserve that.
		/// </summary>
		public static PerformanceSample AdvancePerformance(IPerformanceMove move, PerformanceSample sample,
			PerformanceClockInput input)
		{
			if (sample.State == PerformanceState.Preparing)
			{
				return sample;
			}

			// Prolong holds the performance at its minimum while the agent is still carrying speed.
			if (!input.Paused && (sample.State != PerformanceState.Performing || !input.Prolong ||
				sample.RunTime + input.Delta < move.MinDuration))
			{
				sample.RunTime += input.Delta;
			}

			if (sample.RunTime >= move.TotalDuration)
			{
				sample.State = PerformanceState.Completed;
			}
			else if (sample.RunTime >= move.MinDuration)
			{
				sample.State = PerformanceState.Finishing;
			}

			sample.Weight = ((sample.RunTime - move.MinDuration) / move.Release).InvertClamped();
			return sample;
		}

		/// <summary>
		/// Cancel fadeout. Seeds <see cref="PerformanceSample.CancelTime"/> from the charge already built so
		/// the weight continues from where it was instead of snapping to full for a frame.
		/// </summary>
		public static PerformanceSample AdvanceCancel(IPerformanceMove move, PerformanceSample sample,
			PerformanceClockInput input)
		{
			if (sample.State == PerformanceState.Preparing && move.CancelDuration > 0f && move.MinCharge > 0f)
			{
				float prepareWeight = Mathf.Clamp01(sample.ChargeTime / move.MinCharge);
				sample.CancelTime = (1f - prepareWeight) * move.CancelDuration;
			}

			sample.State = PerformanceState.Finishing;
			sample.CancelTime += input.CancelDelta;

			if (sample.CancelTime >= move.CancelDuration)
			{
				sample.State = PerformanceState.Completed;
			}

			sample.Weight = (sample.CancelTime / move.CancelDuration).InvertClamped();
			return sample;
		}
	}
}
