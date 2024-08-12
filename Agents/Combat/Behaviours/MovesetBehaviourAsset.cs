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
		private IMovePerformanceHandler movePerformanceHandler;

		public void InjectDependencies(RuntimeItemData runtimeItemData, IMovePerformanceHandler movePerformanceHandler)
		{
			this.runtimeItemData = runtimeItemData;
			this.movePerformanceHandler = movePerformanceHandler;
		}

		public override void Start()
		{
			base.Start();
			foreach (ActMovePair pair in moveSet)
			{
				movePerformanceHandler.AddMove(pair.Act, runtimeItemData, PerformanceState.Inactive | PerformanceState.Finishing | PerformanceState.Completed, pair.Move, pair.Prio);
			}
		}

		public override void Stop()
		{
			base.Stop();
			foreach (ActMovePair pair in moveSet)
			{
				movePerformanceHandler.RemoveMove(pair.Act, runtimeItemData);
			}
		}
	}
}
