using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace SpaxUtils.UI
{
	/// <summary>
	/// UI menu behaviour that can be populated with <see cref="Option"/>s.
	/// </summary>
	public class OptionsMenuUI : MonoBehaviour
	{
		public event Action<Option> SelectedOptionEvent;

		// ItemMenu is created on demand because it can be requested before the object is ever enabled, meaning we can't wait until Awake().
		private ItemMenu<Option> Menu
		{
			get
			{
				if (_menu == null)
				{
					_menu = new ItemMenu<Option>(menuItemTemplate, (o) => o.Title, (o) => o.Title, (o) => null);
					_menu.SelectedItemEvent += OnSelectedItem;
				}
				return _menu;
			}
		}

		[SerializeField] private MenuItem menuItemTemplate;
		[SerializeField] private TMP_Text menuTitle;

		private GameService gameService;
		private PlayerInputWrapper playerInputWrapper;
		private ICommunicationChannel comms;

		private ItemMenu<Option> _menu;
		private List<Option> menuOptions = new List<Option>();

		public void InjectDependencies(GameService gameService, PlayerInputWrapper playerInputWrapper, ICommunicationChannel comms)
		{
			this.gameService = gameService;
			this.playerInputWrapper = playerInputWrapper;
			this.comms = comms;
		}

		protected void OnDestroy()
		{
			Menu.Dispose();
		}

		/// <summary>
		/// Initialize the menu's options through a <see cref="RequestOptionsMsg{T}"/> sent over the injected communication channel.
		/// </summary>
		/// <param name="context">The context to utilize for the options request.</param>
		/// <param name="addCancel">Whether a cancel option should be added as the last option.</param>
		/// <param name="title">The menu's title, if any.</param>
		public void Initialize(string context, bool addCancel = false, string title = "")
		{
			var request = RequestOptionsMsg<OptionsMenuUI>.New(this, context, comms);
			Initialize(request.Options, addCancel, title);
		}

		/// <summary>
		/// Initialize this menu with <paramref name="options"/>
		/// </summary>
		/// <param name="options"></param>
		/// <param name="addCancel"></param>
		/// <param name="title"></param>
		public void Initialize(IEnumerable<Option> options, bool addCancel = false, string title = "")
		{
			CleanMenu();

			// Collect Options.
			menuOptions = new List<Option>(options);
			if (addCancel)
			{
				menuOptions.Add(new Option("Cancel", "Close options menu", null, InputActions.CANCEL));
			}

			// Subscribe.
			foreach (Option option in menuOptions)
			{
				option.PickedEvent += OnPickedOption;
				option.Enable(playerInputWrapper);
			}

			// Visuals.
			if (menuTitle != null && !string.IsNullOrEmpty(title))
			{
				menuTitle.text = title;
			}

			Menu.Populate(menuOptions);
		}

		/// <summary>
		/// Manually pick the currently highlighted option.
		/// </summary>
		public void SelectCurrentOption()
		{
			foreach (KeyValuePair<string, (Option data, MenuItem visual)> item in Menu.Items)
			{
				if (gameService.EventSystem.currentSelectedGameObject == item.Value.visual.Button.gameObject)
				{
					item.Value.visual.Button.onClick.Invoke();
					return;
				}
			}
		}

		private void CleanMenu()
		{
			// Unsubscribe.
			foreach (Option option in menuOptions)
			{
				option.PickedEvent -= OnPickedOption;
				//option.Disable();
				option.Dispose();
			}
			Menu.Clear();
		}

		private void OnSelectedItem(Option option)
		{
			option.Pick();
		}

		private void OnPickedOption(Option option)
		{
			//Hide();
			SelectedOptionEvent?.Invoke(option);
		}
	}
}
