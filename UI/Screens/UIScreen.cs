using UnityEngine;

namespace SpaxUtils.UI
{
	public class UIScreen : UIGroup
	{
		public string Context => context;
		public string Shortcut => shortcut;
		public bool Pause => pauseGame;

		[Header("Screen")]
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers), true)] private string context;
		[SerializeField, ConstDropdown(typeof(IInputActions), true)] private string shortcut;
		[SerializeField] private bool pauseGame;
	}
}
