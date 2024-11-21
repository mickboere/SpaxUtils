using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;
using SpaxUtils.UI;
using System;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(GameData), menuName = "ScriptableObjects/GameData")]
	public class GameData : ScriptableObject, IService
	{
		[Serializable]
		public class GameState
		{
			public string State => state;
			public bool HasScene => !string.IsNullOrEmpty(scene);
			public string Scene => scene;

			[SerializeField, ConstDropdown(typeof(IStateIdentifiers))] private string state;
			[SerializeField] private string scene;
		}

		public BrainGraph GameBrainGraph => gameBrainGraph;
		public float TransitionTime => transitionTime;
		public IReadOnlyDictionary<string, GameState> StateData
		{
			get
			{
				if (_stateData == null)
				{
					_stateData = new Dictionary<string, GameState>();
					foreach (GameState gameState in stateData)
					{
						_stateData.Add(gameState.State, gameState);
					}
				}
				return _stateData;
			}
		}
		private Dictionary<string, GameState> _stateData;
		public IList<string> Levels => levels;

		[SerializeField] private BrainGraph gameBrainGraph;
		[SerializeField] private float transitionTime = 3f;
		[SerializeField] private List<GameState> stateData;
		[SerializeField] private List<string> levels;
	}
}
