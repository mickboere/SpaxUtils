using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace SpaxUtils
{
	/// <summary>
	/// Library that matches input bindings with icons.
	/// </summary>
	[CreateAssetMenu(fileName = "InputIconLibrary", menuName = "ScriptableObjects/Input Icon Library")]
	public class InputIconLibrary : ScriptableObject
	{
		[SerializeField] private GamepadIcons xbox;
		[SerializeField] private GamepadIcons playstation;

		public Sprite GetIcon(InputAction action, string controlScheme)
		{
			// Retrieve the first binding in this action that is active for the given control scheme.
			InputBinding binding = action.bindings.FirstOrDefault((b) => b.groups.Contains(controlScheme));
			return GetIcon(binding);
		}

		public Sprite GetIcon(InputBinding inputBinding)
		{
			if (inputBinding == default(InputBinding))
			{
				return null;
			}

			string displayString = inputBinding.ToDisplayString(out string deviceLayoutName, out string controlPath);
			return GetIcon(deviceLayoutName, controlPath);
		}

		public Sprite GetIcon(string deviceLayoutName, string controlPath)
		{
			if (string.IsNullOrEmpty(deviceLayoutName) || string.IsNullOrEmpty(controlPath))
			{
				return null;
			}

			Sprite icon = default(Sprite);
			if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "DualShockGamepad"))
			{
				icon = playstation.GetSprite(controlPath);
			}
			else if (InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Gamepad"))
			{
				icon = xbox.GetSprite(controlPath);
			}

			return icon;
		}

		[Serializable]
		public struct GamepadIcons
		{
			public Sprite buttonSouth;
			public Sprite buttonNorth;
			public Sprite buttonEast;
			public Sprite buttonWest;
			public Sprite startButton;
			public Sprite selectButton;
			public Sprite leftTrigger;
			public Sprite rightTrigger;
			public Sprite leftShoulder;
			public Sprite rightShoulder;
			public Sprite dpad;
			public Sprite dpadUp;
			public Sprite dpadDown;
			public Sprite dpadLeft;
			public Sprite dpadRight;
			public Sprite leftStick;
			public Sprite rightStick;
			public Sprite leftStickPress;
			public Sprite rightStickPress;

			public Sprite GetSprite(string controlPath)
			{
				// From the input system, we get the path of the control on device. So we can just
				// map from that to the sprites we have for gamepads.
				switch (controlPath)
				{
					case "buttonSouth": return buttonSouth;
					case "buttonNorth": return buttonNorth;
					case "buttonEast": return buttonEast;
					case "buttonWest": return buttonWest;
					case "start": return startButton;
					case "select": return selectButton;
					case "leftTrigger": return leftTrigger;
					case "rightTrigger": return rightTrigger;
					case "leftShoulder": return leftShoulder;
					case "rightShoulder": return rightShoulder;
					case "dpad": return dpad;
					case "dpad/up": return dpadUp;
					case "dpad/down": return dpadDown;
					case "dpad/left": return dpadLeft;
					case "dpad/right": return dpadRight;
					case "leftStick": return leftStick;
					case "rightStick": return rightStick;
					case "leftStickPress": return leftStickPress;
					case "rightStickPress": return rightStickPress;
				}
				return null;
			}
		}
	}
}
