using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Tab header of the settings screen; its toggle's checked graphic marks the open tab.
	/// </summary>
	public class SettingsTabUI : MonoBehaviour
	{
		[SerializeField] private Toggle toggle;
		[SerializeField] private TMP_Text label;

		public void Setup(string title, ToggleGroup group, Action onSelected)
		{
			label.text = title;
			toggle.SetIsOnWithoutNotify(false);
			toggle.group = group;
			toggle.onValueChanged.AddListener((on) =>
			{
				if (on)
				{
					onSelected();
				}
			});
		}

		public void SetSelected(bool selected)
		{
			toggle.SetIsOnWithoutNotify(selected);
		}
	}
}
