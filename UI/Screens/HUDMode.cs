using UnityEngine;

namespace SpaxUtils.UI
{
	/// <summary>
	/// One of the HUD layouts a <see cref="HUDManager"/> swaps between.
	/// </summary>
	public class HUDMode : UIGroup
	{
		public string Mode => mode;

		[Header("HUD")]
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers))] private string mode;

		public override void SelectFirstSelectable()
		{
			// The HUD never takes the UI selection.
		}
	}
}
