using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Row for <see cref="BoolSetting"/>.
	/// </summary>
	public class ToggleSettingRowUI : SettingRowUI
	{
		public override Selectable Selectable => toggle;

		[SerializeField] private Toggle toggle;

		public override void Bind(Setting setting, int player)
		{
			toggle.onValueChanged.AddListener(OnToggleChanged);
			base.Bind(setting, player);
		}

		protected override void RefreshValue()
		{
			toggle.SetIsOnWithoutNotify(((BoolSetting)Setting).Get(Player));
		}

		private void OnToggleChanged(bool value)
		{
			Apply(() => ((BoolSetting)Setting).Set(value, Player));
		}
	}
}
