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

		private IDependencyManager dependencyManager;
		private TimeService timeService;
		private PlayerInputWrapper playerInputWrapper;
		private UIScreen[] screens;
		
		private List<Option> shortcuts = new List<Option>();

		public void InjectDependencies(IDependencyManager dependencyManager, TimeService timeService, PlayerInputWrapper playerInputWrapper)
		{
			this.dependencyManager = dependencyManager;
			this.timeService = timeService;
			this.playerInputWrapper = playerInputWrapper;
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
			SwitchContext(defaultContext);
		}

		protected void OnDestroy()
		{
			playerInputWrapper.CompleteActionMapRequest(this);
			timeService.CompletePauseRequest(this);

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
					if (screen.Pause)
					{
						timeService.RequestPause(this);
					}
					else
					{
						timeService.CompletePauseRequest(this);
					}
				}
				else
				{
					screen.Hide();
				}
			}

			this.context = context;
		}
	}
}
