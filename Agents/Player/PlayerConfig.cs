using SpaxUtils;
using SpaxUtils.UI;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SpaxUtils
{
	[Serializable]
	public class PlayerConfig
	{
		public AgentSetupAsset AgentSetup => agentSetup;
		public InputActionAsset InputActionAsset => inputActionAsset;
		public GameObject CameraPrefab => cameraPrefab;
		public UIRoot UIPrefab => uiPrefab;
		public MultiplayerEventSystem PlayerEventSystemPrefab => playerEventSystemPrefab;

		[SerializeField] private AgentSetupAsset agentSetup;
		[SerializeField] private InputActionAsset inputActionAsset;
		[SerializeField] private GameObject cameraPrefab;
		[SerializeField] private UIRoot uiPrefab;
		[SerializeField, Tooltip("Gives each player their own UI selection and navigation. Leave empty to share the global event system.")]
		private MultiplayerEventSystem playerEventSystemPrefab;
	}
}
