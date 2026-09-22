using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Row for any <see cref="IOptionSetting"/>, listing every option at once.
	/// </summary>
	public class DropdownSettingRowUI : SettingRowUI
	{
		public override Selectable Selectable => dropdown;
		public bool IsExpanded => dropdown.IsExpanded;

		[SerializeField] private TMP_Dropdown dropdown;

		private readonly List<string> labels = new List<string>();

		public override void Bind(Setting setting, int player)
		{
			dropdown.onValueChanged.AddListener(OnDropdownChanged);
			base.Bind(setting, player);
		}

		protected override void RefreshValue()
		{
			IOptionSetting options = (IOptionSetting)Setting;

			// Options can be hardware dependent (resolutions, monitors), so rebuild them every refresh.
			labels.Clear();
			labels.AddRange(options.GetLabels());
			dropdown.ClearOptions();
			dropdown.AddOptions(labels);
			dropdown.SetValueWithoutNotify(Mathf.Max(0, options.GetIndex(Player)));
		}

		private void OnDropdownChanged(int index)
		{
			Apply(() => ((IOptionSetting)Setting).SetIndex(index, Player));
		}
	}
}
