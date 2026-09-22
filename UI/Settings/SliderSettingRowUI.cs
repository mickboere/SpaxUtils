using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Row for <see cref="FloatSetting"/> and <see cref="IntSetting"/>.
	/// </summary>
	public class SliderSettingRowUI : SettingRowUI
	{
		private const string DEFAULT_FORMAT = "0.##";

		public override Selectable Selectable => slider;

		[SerializeField] private Slider slider;
		[SerializeField] private TMP_Text valueText;

		public override void Bind(Setting setting, int player)
		{
			SettingAttribute info = setting.Info;
			slider.wholeNumbers = setting is IntSetting;
			slider.minValue = info.HasRange ? info.Min : 0f;
			slider.maxValue = info.HasRange ? info.Max : 1f;
			slider.onValueChanged.AddListener(OnSliderChanged);
			base.Bind(setting, player);
		}

		protected override void RefreshValue()
		{
			float value = GetValue();
			slider.SetValueWithoutNotify(value);
			if (valueText != null)
			{
				valueText.text = value.ToString(Setting.Info.Format ?? DEFAULT_FORMAT, CultureInfo.InvariantCulture);
			}
		}

		private float GetValue()
		{
			return Setting switch
			{
				FloatSetting f => f.Get(Player),
				IntSetting i => i.Get(Player),
				_ => 0f
			};
		}

		private void OnSliderChanged(float value)
		{
			Apply(() =>
			{
				if (Setting is FloatSetting f)
				{
					f.Set(value, Player);
				}
				else if (Setting is IntSetting i)
				{
					i.Set(Mathf.RoundToInt(value), Player);
				}
			});
		}
	}
}
