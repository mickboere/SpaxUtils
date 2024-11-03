using SpaxUtils.StateMachines;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(GameData), menuName = "ScriptableObjects/GameData")]
	public class GameData : ScriptableObject, IService
	{
		public UIRoot UIRoot;

		public string CharacterCreatorScene = "CharacterCreator";
		public string FirstLevelScene = "";
	}
}
