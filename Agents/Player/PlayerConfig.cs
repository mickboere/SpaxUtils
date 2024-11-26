using SpaxUtils;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaxUtils
{
	[Serializable]
	public class PlayerConfig
	{
		public AgentSetupAsset AgentSetup => agentSetup;
		public InputActionAsset InputActionAsset => inputActionAsset;
		public GameObject CameraPrefab => cameraPrefab;
		public GameObject UIPrefab => uiPrefab;

		[SerializeField] private AgentSetupAsset agentSetup;
		[SerializeField] private InputActionAsset inputActionAsset;
		[SerializeField] private GameObject cameraPrefab;
		[SerializeField] private GameObject uiPrefab;
	}
}
