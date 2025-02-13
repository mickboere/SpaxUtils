using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;
using SpaxUtils.UI;
using System;
using UnityEngine.EventSystems;

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

		[Serializable]
		public class LevelData
		{
			public string Name => name;
			public FlowGraph FlowGraph => flowGraph;

			[SerializeField] private string name;
			[SerializeField] private FlowGraph flowGraph;
		}

		public EventSystem EventSystem => eventSystem;
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
		public IReadOnlyDictionary<string, LevelData> Levels
		{
			get
			{
				if (_levels == null)
				{
					_levels = new Dictionary<string, LevelData>();
					foreach (LevelData levelData in levels)
					{
						_levels.Add(levelData.Name, levelData);
					}
				}
				return _levels;
			}
		}
		private Dictionary<string, LevelData> _levels;

		[SerializeField] private EventSystem eventSystem;
		[SerializeField] private BrainGraph gameBrainGraph;
		[SerializeField] private float transitionTime = 3f;
		[SerializeField] private List<GameState> stateData;
		[SerializeField] private List<LevelData> levels;
	}
}
