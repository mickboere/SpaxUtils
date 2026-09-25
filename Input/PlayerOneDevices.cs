using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// The devices player one owns in a level; keyboard and mouse always belong to player one.
	/// </summary>
	public enum PlayerOneDevices
	{
		[InspectorName("Keyboard, Mouse & Gamepad")] KeyboardMouseAndGamepad, // Plus the first gamepad used.
		[InspectorName("Keyboard & Mouse Only")] KeyboardAndMouseOnly // Every gamepad joins with Start.
	}
}
