using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using static UnityEngine.InputSystem.InputAction;

namespace SpaxUtils.UI
{
	/// <summary>
	/// UI component that handles showing the correct menu to the player.
	/// </summary>
	public class PlayerMenuManager : MonoBehaviour, IDependency
	{
		[Serializable]
		public class PlayerMenu
		{
			public UIGroup Menu => menu;
			public bool CrossFade => crossfade;
			public bool Immediate => immediate;
			public string InputCommand => openCommand;
			public bool EnableShortcuts => enableShortcuts;
			public bool Pause => pause;

			[SerializeField] private UIGroup menu;
			[SerializeField] private bool crossfade = false;
			[SerializeField] private bool immediate = false;
			[SerializeField, ConstDropdown(typeof(IInputActions), true)] private string openCommand;
			[SerializeField] private bool enableShortcuts;
			[SerializeField] private bool pause;
		}

		[SerializeField] private List<PlayerMenu> menus;
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers), true)] private string defaultMenu;

		private IAgent agent;
		private CursorService cursorService;
		private PlayerInputWrapper playerInputWrapper;
		private TimeService timeService;
		private ICommunicationChannel comms;
		private RuntimeDataService runtimeDataService;

		private PlayerMenu currentMenu;
		private MenuNavigationHelper navigationHelper;

		public void InjectDependencies(
			CursorService cursorService,
			PlayerInputWrapper playerInputWrapper,
			TimeService timeService,
			ICommunicationChannel comms,
			RuntimeDataService runtimeDataService,
			[Optional] IAgent agent)
		{
			this.cursorService = cursorService;
			this.playerInputWrapper = playerInputWrapper;
			this.timeService = timeService;
			this.comms = comms;
			this.runtimeDataService = runtimeDataService;
			this.agent = agent;
		}

		protected void OnEnable()
		{
			// NavigationHelper takes care of hiding/showing the correct menus.
			navigationHelper = new MenuNavigationHelper(menus.Select((m) => m.Menu).ToList());

			//playerInputWrapper.RequestActionMaps(this, 0, InputActionMaps.ACTION_MAP_MENUS);

			// We need to update our cursor lock whenever the last input device changes.
			playerInputWrapper.LastInputDeviceChangedEvent += OnLastDeviceChangedEvent;

			// We want to supply options for the pause menu depending on our configured menus.
			//comms.Listen<RequestOptionsMsg<OptionsMenuUI>>(this, OnRequestMenuOptionsMsg);

			SwitchPlayerMenu(null);
			//if (!string.IsNullOrEmpty(defaultMenu))
			//{
			//	PlayerMenu def = GetPlayerMenu(defaultMenu);
			//	if (def == null)
			//	{
			//		SpaxDebug.Error($"No menu of type '{defaultMenu}' is defined.");
			//	}
			//	else
			//	{
			//		SwitchPlayerMenu(def);
			//	}
			//}
		}

		protected void OnDisable()
		{
			navigationHelper.Dispose();
			playerInputWrapper.LastInputDeviceChangedEvent -= OnLastDeviceChangedEvent;
			playerInputWrapper.CompleteActionMapRequest(this);
			playerInputWrapper.CompleteActionMapRequest(currentMenu);
			DisableShortcuts();
			comms.StopListening(this);

			Pause(false);
		}

		//public PlayerMenu GetPlayerMenu(string menuType)
		//{
		//	return menus.FirstOrDefault((m) => m.Menu.MenuType == menuType);
		//}

		public void SwitchMenu(string menuType, Action callback = null)
		{
			//SwitchPlayerMenu(GetPlayerMenu(menuType), callback);
		}

		public void SwitchPlayerMenu(PlayerMenu menu, Action callback = null)
		{
			if (menu == currentMenu)
			{
				// Menu is already active, toggle it.
				menu = null;
			}

			//SpaxDebug.Notify("SwitchPlayerMenu: ", $"{(menu == null ? "null" : menu.Menu.MenuType)}");

			playerInputWrapper.CompleteActionMapRequest(currentMenu);
			currentMenu = menu;
			DisableShortcuts();
			if (menu == null || menu.EnableShortcuts)
			{
				EnableShortcuts();
			}

			if (menu == null)
			{
				navigationHelper.TransitionTo(null, false, false, callback);
			}
			else
			{
				navigationHelper.TransitionTo(menu.Menu, menu.CrossFade, menu.Immediate, () =>
				{
					menu.Menu.SelectFirstSelectable();
					callback?.Invoke();
				});
				//playerInputWrapper.RequestActionMaps(menu, 100, InputActionMaps.ACTION_MAP_UI, InputActionMaps.ACTION_MAP_MENUS);
			}

			Pause(menu != null && menu.Pause);
			UpdateCursorLock();
		}

		private void Pause(bool pause)
		{
			if (pause)
			{
				timeService.RequestPause(this);
			}
			else
			{
				timeService.CompletePauseRequest(this);
			}
		}

		private void OnLastDeviceChangedEvent(InputDevice device)
		{
			UpdateCursorLock();
		}

		private void UpdateCursorLock()
		{
			// Only unlock the cursor if any menu is opened and the current control scheme is Keyboard&Mouse.
			bool lockCursor = !(currentMenu != null && playerInputWrapper.CurrentControlScheme == ControlSchemes.KEYBOARD_AND_MOUSE);
			cursorService.LockCursor(this, lockCursor);
		}

		private void EnableShortcuts()
		{
			// For each menu that has a dedicated input command we subscribe to the input wrapper.
			foreach (PlayerMenu menu in menus)
			{
				if (!string.IsNullOrWhiteSpace(menu.InputCommand))
				{
					playerInputWrapper.Subscribe(this, menu.InputCommand,
						delegate (CallbackContext inputContext)
						{
							//SpaxDebug.Log($"OnActionTriggered: [{menu.InputCommand}] ", $"Phase={inputContext.phase}", callerIndex: 2);
							if (inputContext.canceled)
							{
								SwitchPlayerMenu(menu);
							}

							// Eat.
							return true;
						});
				}
			}
		}

		private void DisableShortcuts()
		{
			playerInputWrapper.Unsubscribe(this);
		}

		//private void OnRequestMenuOptionsMsg(RequestOptionsMsg<OptionsMenuUI> msg)
		//{
		//	// If the OptionsMenu requesting options is the pause menu, provide it with all configured menus.
		//	if (agent != null && msg.Context == MenuContexts.PAUSE)
		//	{
		//		// Add Resume option.
		//		msg.AddOption(new Option("Resume", "", (option) => SwitchPlayerMenu(null), GetPlayerMenu(MenuContexts.PAUSE).InputCommand));

		//		// Add saving option.
		//		msg.AddOption(new Option("Save", "", (option) =>
		//		{
		//			agent.SaveData();
		//			runtimeDataService.SaveProfileToDisk();
		//			SwitchPlayerMenu(null);
		//		}));

		//		// Add all other menus as options.
		//		foreach (PlayerMenu menu in menus)
		//		{
		//			if (menu.Menu.MenuType != MenuContexts.PAUSE)
		//			{
		//				msg.AddOption(new Option(menu.Menu.MenuType, "", (option) => SwitchPlayerMenu(menu), menu.InputCommand));
		//			}
		//		}

		//		// Add Quit option.
		//		msg.AddOption(new Option("Quit", "", (option) =>
		//		{
		//			SpaxDebug.Error("Quiting is not implemented.");
		//			SwitchPlayerMenu(null);
		//		}));
		//	}
		//}
	}
}
