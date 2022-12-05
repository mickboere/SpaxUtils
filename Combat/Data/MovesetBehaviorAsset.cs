using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IBehaviour"/> asset that modifies the current moveset.
	/// </summary>
	[CreateAssetMenu(fileName = "Moveset", menuName = "ScriptableObjects/Moveset Behavior")]
	public class MovesetBehaviorAsset : BehaviorAsset
	{
		[SerializeField] private int prio;
		[SerializeField] private List<ActCombatPair> moveSet;

		private ICombatPerformer combatPerformer;

		public void InjectDependencies(ICombatPerformer combatPerformer)
		{
			this.combatPerformer = combatPerformer;
		}

		public override void Start()
		{
			base.Start();
			foreach (ActCombatPair combatAct in moveSet)
			{
				combatPerformer.AddCombatMove(combatAct.Act, combatAct.Move, prio);
			}
		}

		public override void Stop()
		{
			base.Stop();
			foreach (ActCombatPair combatAct in moveSet)
			{
				combatPerformer.RemoveCombatMove(combatAct.Act, combatAct.Move);
			}
		}
	}
}
