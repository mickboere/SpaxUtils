using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Inherits <see cref="CorePerformanceMoveBehaviourAsset"/>, implements default combat move pose evaluation.
	/// </summary>
	public class BaseCombatMoveBehaviourAsset : CorePerformanceMoveBehaviourAsset
	{
		protected override IPoserInstructions Evaluate(out float weight)
		{
			if (Move.PosingData is PoseSequence sequence)
			{
				// Charging.
				IPose chargePose = sequence.Get(0);
				float chargeWeight = chargePose.EvaluateTransition(Mathf.Clamp01(Performer.PrepTime / Move.MaxPrep));

				// Performing.
				if (Move.HasPerformance)
				{
					float chargeToPerformanceTransition = (Performer.RunTime / (Move.MinDuration * Move.ChargeFadeout)).Clamp01().InOutSine();
					float performanceWeight = ((Performer.RunTime - Move.MinDuration) / Move.Release).InvertClamped().InOutSine();
					weight = Mathf.Lerp(chargeWeight, performanceWeight, chargeToPerformanceTransition).Lerp(0f, Mathf.Clamp01(Performer.CancelTime / Move.CancelDuration));
				}
				else
				{
					weight = chargeWeight * Performer.Weight;
				}

				return new PoserInstructions(sequence.Evaluate(Move.HasPerformance ? Performer.RunTime : 0f));
			}
			else
			{
				SpaxDebug.Error("Behaviour only supports PoseSequence as PosingData", $"Selected: {Move.PosingData.GetType().FullName}");
				weight = 0f;
				return null;
			}
		}
	}
}
