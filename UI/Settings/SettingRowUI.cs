using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// One labelled row of the settings screen, presenting and editing a single <see cref="Setting"/>.
	/// </summary>
	public abstract class SettingRowUI : MonoBehaviour
	{
		/// <summary>
		/// Receives changes to <see cref="SettingAttribute.Confirm"/> settings so they can be confirmed or reverted.
		/// </summary>
		public Action<SettingRowUI, Action> ConfirmHandler { get; set; }

		public Setting Setting { get; private set; }
		public int Player { get; private set; }
		public abstract Selectable Selectable { get; }

		[SerializeField] protected TMP_Text label;
		[SerializeField, Tooltip("Shown while the value differs from its default.")]
		protected GameObject modifiedMarker;

		protected virtual bool IsModified => !Setting.IsDefault(Player);

		public virtual void Bind(Setting setting, int player)
		{
			Setting = setting;
			Player = player;
			if (label != null)
			{
				label.text = setting.Info.Label;
			}
			setting.ChangedEvent += OnSettingChanged;
			Refresh();
		}

		protected virtual void OnDestroy()
		{
			if (Setting != null)
			{
				Setting.ChangedEvent -= OnSettingChanged;
			}
		}

		public virtual void ResetToDefault()
		{
			Apply(() => Setting.ResetToDefault(Player));
		}

		public void Refresh()
		{
			RefreshValue();
			if (modifiedMarker != null)
			{
				modifiedMarker.SetActive(IsModified);
			}
		}

		protected abstract void RefreshValue();

		/// <summary>
		/// Runs <paramref name="change"/>, through the confirm handler when the setting requires confirmation.
		/// </summary>
		protected void Apply(Action change)
		{
			if (Setting.Info.Confirm && ConfirmHandler != null)
			{
				ConfirmHandler(this, change);
			}
			else
			{
				change();
			}
		}

		private void OnSettingChanged(Setting setting, int player)
		{
			if (!setting.PerPlayer || player == Player)
			{
				Refresh();
			}
		}
	}
}
