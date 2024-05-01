using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Inherits <see cref="BasePerformanceMoveBehaviourAsset"/>, implements default combat move pose evaluation.
	/// </summary>
	public class BaseCombatMoveBehaviourAsset : BasePerformanceMoveBehaviourAsset
	{
		protected override IPoserInstructions Evaluate(out float weight)
		{
			if (Move.PosingData is PoseSequence sequence)
			{
				// Charging.
				IPose chargePose = sequence.Get(0);
				float chargeWeight = chargePose.EvaluateTransition(Mathf.Clamp01(Performer.Charge / Move.MaxCharge));

				// Performing.
				float performanceWeight = Move.HasPerformance ? Mathf.Clamp01(Performer.RunTime / (Move.MinDuration * Move.ChargeFadeout)).InOutSine() : Mathf.Sign(Performer.RunTime);
				weight = Mathf.Lerp(chargeWeight, 1f - Mathf.Clamp01((Performer.RunTime - Move.MinDuration) / Move.Release).InOutSine(), performanceWeight).Lerp(0f, Performer.CancelTime / Move.CancelDuration);

				return new PoserInstructions(sequence.Evaluate(Move.HasPerformance ? Performer.RunTime : 0f));
			}
			else
			{
				weight = 0f;
				return null;
			}
		}
	}
}
