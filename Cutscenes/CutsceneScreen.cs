using UnityEngine;
using UnityEngine.UI;
using SpaxUtils;
using SpaxUtils.UI;
using static UnityEngine.InputSystem.InputAction;

namespace SpiritAxis
{
	/// <summary>
	/// UIScreen shown during cutscene playback. Handles the double-press skip input:
	/// first press shows the skip prompt, second press triggers the skip.
	/// </summary>
	public class CutsceneScreen : UIScreen
	{
		[Header("Skip Prompt")]
		[SerializeField] private UIGroup skipPromptGroup;
		[SerializeField] private Button skipPromptButton;

		private CutsceneService cutsceneService;
		private PlayerInputWrapper playerInputWrapper;

		private Option skipOption;
		private bool promptShown;

		public void InjectDependencies(CutsceneService cutsceneService, PlayerInputWrapper playerInputWrapper)
		{
			this.cutsceneService = cutsceneService;
			this.playerInputWrapper = playerInputWrapper;
		}

		protected void Awake()
		{
			skipPromptGroup.HideImmediately();

			if (skipPromptButton != null)
			{
				skipPromptButton.onClick.AddListener(OnSkipPromptClicked);
				skipPromptButton.interactable = false;
			}
		}

		protected override void OnDestroy()
		{
			DisableSkipOption();

			if (skipPromptButton != null)
			{
				skipPromptButton.onClick.RemoveListener(OnSkipPromptClicked);
			}

			base.OnDestroy();
		}

		protected override void OnShow()
		{
			base.OnShow();
			promptShown = false;
			EnableSkipOption();
		}

		protected override void OnHide()
		{
			base.OnHide();
			DisableSkipOption();
			skipPromptGroup.HideImmediately();
			promptShown = false;

			if (skipPromptButton != null)
			{
				skipPromptButton.interactable = false;
			}
		}

		private void OnSkipInput(CallbackContext context)
		{
			if (!context.performed)
			{
				return;
			}

			if (!promptShown)
			{
				// First press: show skip prompt.
				promptShown = true;

				if (skipPromptButton != null)
				{
					skipPromptButton.interactable = true;
				}

				skipPromptGroup.Show();
			}
			else
			{
				// Second press: skip the cutscene.
				PerformSkip();
			}
		}

		private void OnSkipPromptClicked()
		{
			if (promptShown)
			{
				PerformSkip();
			}
		}

		private void PerformSkip()
		{
			if (cutsceneService != null)
			{
				cutsceneService.Skip();
			}
		}

		private void EnableSkipOption()
		{
			if (skipOption == null)
			{
				skipOption = new Option("Skip", InputActions.SUBMIT, OnSkipInput, playerInputWrapper);
			}

			skipOption.Enable();
		}

		private void DisableSkipOption()
		{
			if (skipOption != null)
			{
				skipOption.Disable();
			}
		}
	}
}
