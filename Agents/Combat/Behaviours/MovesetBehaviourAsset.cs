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
			Apply(movePerformanceHandler);
		}

		public override void Stop()
		{
			base.Stop();
			Revoke(movePerformanceHandler);
		}

		public void Apply(IMovePerformanceHandler handler)
		{
			if (handler == null) return;
			foreach (ActMovePair pair in moveSet)
			{
				handler.AddMove(pair.Act, pair.Move, PerformanceState.Inactive | PerformanceState.Finishing | PerformanceState.Completed, pair.Prio, null, pair.Conditions);
			}
		}

		public void Revoke(IMovePerformanceHandler handler)
		{
			if (handler == null) return;
			foreach (ActMovePair pair in moveSet)
			{
				handler.RemoveMove(pair.Act, pair.Move);
			}
		}
	}
}
