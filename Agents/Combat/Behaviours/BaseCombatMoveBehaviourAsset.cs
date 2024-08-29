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
				if (Move.HasPerformance)
				{
					float performanceWeight = (Performer.RunTime / (Move.MinDuration * Move.ChargeFadeout)).Clamp01().InOutSine();
					weight = Mathf.Lerp(chargeWeight, Mathf.Clamp01((Performer.RunTime - Move.MinDuration) / Move.Release).Invert().InOutSine(), performanceWeight).LerpTo(0f, Performer.CancelTime / Move.CancelDuration);
				}
				else
				{
					weight = chargeWeight.LerpTo(0f, Performer.CancelTime / Move.CancelDuration);
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
