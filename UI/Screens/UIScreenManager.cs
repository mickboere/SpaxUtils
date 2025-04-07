using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.InputSystem.InputAction;

namespace SpaxUtils.UI
{
	public class UIScreenManager : MonoBehaviour, IDependency
	{
		[SerializeField, ReadOnly] private string context;
		[SerializeField] private RectTransform screenParent;
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers), true)] private string defaultContext;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string shortcutActionMap;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string uiActionMap;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string navigationActionMap;

		private string baseContextOverride = null;
		private object subscriberA = new object();
		private object subscriberB = new object();

		private ICommunicationChannel comms;
		private TimeService timeService;
		private PlayerInputWrapper playerInputWrapper;
		private CursorService cursorService;

		private UIScreen[] screens;
		private List<Option> shortcuts = new List<Option>();

		public void InjectDependencies(ICommunicationChannel comms, TimeService timeService, PlayerInputWrapper playerInputWrapper, CursorService cursorService)
		{
			this.comms = comms;
			this.timeService = timeService;
			this.playerInputWrapper = playerInputWrapper;
			this.cursorService = cursorService;
		}

		protected void Awake()
		{
			screens = screenParent.GetComponentsInChildren<UIScreen>(true);
			foreach (UIScreen screen in screens)
			{
				if (!string.IsNullOrEmpty(screen.Shortcut))
				{
					shortcuts.Add(new Option(screen.Context, screen.Shortcut, (CallbackContext c) => { if (c.canceled) SwitchContext(screen.Context); }, playerInputWrapper, enable: true));
				}
				screen.gameObject.SetActive(true);
				screen.gameObject.SetActive(false);
			}
			comms.Listen<SwitchBaseContextMsg>(this, OnSwitchBaseContextMsg);
			playerInputWrapper.RequestActionMaps(subscriberA, 0, shortcutActionMap);
			SwitchContext(defaultContext);
		}

		protected void OnDestroy()
		{
			playerInputWrapper.CompleteActionMapRequest(subscriberA);
			playerInputWrapper.CompleteActionMapRequest(subscriberB);
			timeService.CompletePauseRequest(this);
			comms.StopListening(this);
			foreach (Option shortcut in shortcuts)
			{
				shortcut.Dispose();
			}
		}

		public void SwitchContext(string context)
		{
			if (context == this.context || context == null)
			{
				// Toggle.
				context = baseContextOverride ?? defaultContext;
			}

			float hideDelay = screens.Max(s => s.TransitionSettings.OutTime * s.Transition.Progress);

			foreach (UIScreen screen in screens)
			{
				if (screen.Context == context)
				{
					screen.Show(null, hideDelay);

					// Pausing:
					if (screen.Pause)
					{
						timeService.RequestPause(this);
					}
					else
					{
						timeService.CompletePauseRequest(this);
					}

					// Input:
					if (screen.RequireInput)
					{
						// Request cursor if current control scheme is mouse and keyboard.
						if (playerInputWrapper.CurrentControlScheme == ControlSchemes.KEYBOARD_AND_MOUSE)
						{
							cursorService.RequestCursor(this);
						}
						playerInputWrapper.RequestActionMaps(subscriberB, 1, shortcutActionMap, uiActionMap, navigationActionMap);
					}
					else
					{
						cursorService.CompleteRequest(this);
						playerInputWrapper.CompleteActionMapRequest(subscriberB);
					}

					// Shortcuts:
					foreach (Option shortcut in shortcuts)
					{
						shortcut.Toggle(screen.EnableShortcuts);
					}
				}
				else
				{
					screen.Hide();
				}
			}

			this.context = context;
		}

		private void OnSwitchBaseContextMsg(SwitchBaseContextMsg msg)
		{
			baseContextOverride = msg.Context;
			SwitchContext(baseContextOverride);
		}
	}
}
