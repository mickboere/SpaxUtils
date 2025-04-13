using SpaxUtils.UI;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(UIGroup))]
	[RequireComponent(typeof(OptionsMenuUI))]
	public class GenericOptionsMenuUI : MonoBehaviour, IDependency
	{
		public UIGroup UIGroup
		{
			get
			{
				if (!_uiGroup)
				{
					_uiGroup = GetComponent<UIGroup>();
				}
				return _uiGroup;
			}
		}
		private UIGroup _uiGroup;

		public OptionsMenuUI OptionsMenu
		{
			get
			{
				if (!_optionsMenu)
				{
					_optionsMenu = GetComponent<OptionsMenuUI>();
					_optionsMenu.SelectedOptionEvent += OnSelectedOptionEvent;
				}
				return _optionsMenu;
			}
		}
		private OptionsMenuUI _optionsMenu;

		[SerializeField] private RectTransform menuRoot;

		public void Show(string context, string title, bool addCancel = false)
		{
			OptionsMenu.Initialize(context, title, addCancel);
			OnShow();
		}

		public void Show(IEnumerable<Option> options, string title, bool addCancel = false)
		{
			OptionsMenu.Initialize(options, title, addCancel);
			OnShow();
		}

		public void Hide()
		{
			OptionsMenu.DisableAllOptions();
			UIGroup.Hide();
		}

		private void OnShow()
		{
			UIGroup.Show();
		}

		private void OnSelectedOptionEvent(Option option)
		{
			Hide();
		}
	}
}
