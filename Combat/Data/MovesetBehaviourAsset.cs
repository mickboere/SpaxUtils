using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IBehaviour"/> asset that modifies the current moveset.
	/// </summary>
	[CreateAssetMenu(fileName = "Moveset", menuName = "ScriptableObjects/Moveset Behaviour")]
	public class MovesetBehaviourAsset : BehaviourAsset
	{
		[SerializeField] private List<ActMovePair> moveSet;

		private IMovePerformer combatPerformer;

		public void InjectDependencies(IMovePerformer combatPerformer)
		{
			this.combatPerformer = combatPerformer;
		}

		public override void Start()
		{
			base.Start();
			foreach (ActMovePair pair in moveSet)
			{
				combatPerformer.AddMove(pair.Act, this, PerformanceState.Inactive | PerformanceState.Finishing | PerformanceState.Completed, pair.Move, pair.Prio);
			}
		}

		public override void Stop()
		{
			base.Stop();
			foreach (ActMovePair pair in moveSet)
			{
				combatPerformer.RemoveMove(pair.Act, this);
			}
		}
	}
}
