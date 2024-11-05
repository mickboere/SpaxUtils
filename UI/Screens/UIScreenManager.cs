using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.InputSystem.InputAction;

namespace SpaxUtils.UI
{
	public class UIScreenManager : MonoBehaviour
	{
		[SerializeField, ReadOnly] private string context;
		[SerializeField] private RectTransform screenParent;
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers), true)] private string defaultContext;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string shortcutActionMap;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string uiActionMap;

		private IAgent agent;
		private ICommunicationChannel comms;
		private TimeService timeService;
		private PlayerInputWrapper playerInputWrapper;
		private CursorService cursorService;
		private RuntimeDataService runtimeDataService;

		private UIScreen[] screens;
		private List<Option> shortcuts = new List<Option>();

		public void InjectDependencies(IAgent agent, ICommunicationChannel comms, TimeService timeService, PlayerInputWrapper playerInputWrapper, CursorService cursorService, RuntimeDataService runtimeDataService)
		{
			this.agent = agent;
			this.comms = comms;
			this.timeService = timeService;
			this.playerInputWrapper = playerInputWrapper;
			this.cursorService = cursorService;
			this.runtimeDataService = runtimeDataService;
		}

		protected void Awake()
		{
			screens = screenParent.GetComponentsInChildren<UIScreen>(true);
			foreach (UIScreen screen in screens)
			{
				if (!string.IsNullOrEmpty(screen.Shortcut))
				{
					shortcuts.Add(new Option(screen.Context, screen.Shortcut, (CallbackContext c) => { SwitchContext(screen.Context); }, playerInputWrapper, enable: true));
				}
			}
			playerInputWrapper.RequestActionMaps(this, 0, shortcutActionMap);
			comms.Listen<IRequestOptionsMsg>(this, OnRequestMenuOptionsMsg); // TODO: Make elegant.
			SwitchContext(defaultContext);
		}

		protected void OnDestroy()
		{
			playerInputWrapper.CompleteActionMapRequest(this);
			timeService.CompletePauseRequest(this);
			comms.StopListening(this);

			foreach (Option shortcut in shortcuts)
			{
				shortcut.Dispose();
			}
		}

		private void SwitchContext(string context)
		{
			if (context == this.context)
			{
				// Toggle.
				context = defaultContext;
			}

			float hideDelay = screens.Max(s => s.TransitionSettings.OutTime * s.Transition.Progress);

			foreach (UIScreen screen in screens)
			{
				if (screen.Context == context)
				{
					screen.Show(null, hideDelay);

					// Pause if screen requires the game to pause.
					if (screen.Pause)
					{
						timeService.RequestPause(this);
					}
					else
					{
						timeService.CompletePauseRequest(this);
					}

					// Request cursor control of screen requires cursor and control scheme is mouse and keyboard.
					if (screen.RequireInput && playerInputWrapper.CurrentControlScheme == ControlSchemes.KEYBOARD_AND_MOUSE)
					{
						cursorService.RequestCursor(this);
						playerInputWrapper.RequestActionMaps(this.context, 0, uiActionMap);
					}
					else
					{
						cursorService.CompleteRequest(this);
						playerInputWrapper.CompleteActionMapRequest(this.context);
					}
				}
				else
				{
					screen.Hide();
				}
			}

			this.context = context;
		}

		private void OnRequestMenuOptionsMsg(IRequestOptionsMsg msg)
		{
			// If the OptionsMenu requesting options is the pause menu, provide it with all configured menus.
			if (msg.Context == ContextIdentifiers.PAUSED)
			{
				// Add Resume option.
				msg.AddOption(new Option("Resume", "", (option) => SwitchContext(defaultContext)));

				// Add saving option.
				msg.AddOption(new Option("Save", "", (option) =>
				{
					agent.SaveData();
					runtimeDataService.SaveProfileToDisk();
					SwitchContext(defaultContext);
				}));

				// Add all other menus as options.
				foreach (UIScreen screen in screens)
				{
					if (screen.Context != ContextIdentifiers.PAUSED)
					{
						msg.AddOption(new Option(screen.Context, "", (option) => SwitchContext(screen.Context)));
					}
				}

				// Add Quit option.
				msg.AddOption(new Option("Quit", "", (option) =>
				{
					Application.Quit();
				}));
			}
		}
	}
}
