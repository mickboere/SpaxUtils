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

		private RuntimeItemData runtimeItemData;
		private IMovePerformer combatPerformer;

		public void InjectDependencies(RuntimeItemData runtimeItemData, IMovePerformer combatPerformer)
		{
			this.runtimeItemData = runtimeItemData;
			this.combatPerformer = combatPerformer;
		}

		public override void Start()
		{
			base.Start();
			foreach (ActMovePair pair in moveSet)
			{
				combatPerformer.AddMove(pair.Act, runtimeItemData, PerformanceState.Inactive | PerformanceState.Finishing | PerformanceState.Completed, pair.Move, pair.Prio);
			}
		}

		public override void Stop()
		{
			base.Stop();
			foreach (ActMovePair pair in moveSet)
			{
				combatPerformer.RemoveMove(pair.Act, runtimeItemData);
			}
		}
	}
}
