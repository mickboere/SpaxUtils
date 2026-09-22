using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Row for one binding of one action, part of a player's <see cref="BindingsSetting"/>.
	/// Submit starts an interactive rebind; a duplicate within the same map and scheme gets swapped.
	/// </summary>
	public class KeybindRowUI : SettingRowUI
	{
		private const float INPUT_BLOCK_TIME = 0.25f;
		private const string WAITING = "...";

		/// <summary>
		/// True while rebinding or just after, so the keys used to finish it don't also trigger menu commands.
		/// </summary>
		public static bool BlockingInput => active != null || Time.unscaledTime < blockUntil;

		private static KeybindRowUI active;
		private static float blockUntil;

		// Statics survive play sessions with domain reload off, while unscaled time restarts at zero.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			active = null;
			blockUntil = 0f;
		}

		public override Selectable Selectable => button;

		[SerializeField] private Button button;
		[SerializeField] private TMP_Text bindingText;
		[SerializeField] private Image icon;

		private PlayerInputSettings inputSettings;
		private InputIconLibrary iconLibrary;
		private InputAction action;
		private int bindingIndex;
		private string controlScheme;
		private InputAction submitAction;
		private InputActionRebindingExtensions.RebindingOperation operation;

		protected override bool IsModified => !string.IsNullOrEmpty(action.bindings[bindingIndex].overridePath);

		public void Bind(BindingsSetting setting, int player, InputAction action, int bindingIndex, string controlScheme,
			InputAction submitAction, PlayerInputSettings inputSettings, InputIconLibrary iconLibrary)
		{
			this.action = action;
			this.bindingIndex = bindingIndex;
			this.controlScheme = controlScheme;
			this.submitAction = submitAction;
			this.inputSettings = inputSettings;
			this.iconLibrary = iconLibrary;
			button.onClick.AddListener(OnClick);
			base.Bind(setting, player);
			label.text = GetLabel(action, bindingIndex);
		}

		protected override void OnDestroy()
		{
			CancelRebind();
			base.OnDestroy();
		}

		public override void ResetToDefault()
		{
			action.RemoveBindingOverride(bindingIndex);
			inputSettings.SaveBindings(Player, action.actionMap.asset);
		}

		/// <summary>
		/// "Move Up" for composite parts, else the action name.
		/// </summary>
		public static string GetLabel(InputAction action, int bindingIndex)
		{
			InputBinding binding = action.bindings[bindingIndex];
			return binding.isPartOfComposite ? $"{action.name} {binding.name.Nicify()}" : action.name;
		}

		protected override void RefreshValue()
		{
			if (operation != null)
			{
				SetDisplay(WAITING, null);
				return;
			}

			InputBinding binding = action.bindings[bindingIndex];
			Sprite sprite = iconLibrary != null ? iconLibrary.GetIcon(binding) : null;
			SetDisplay(action.GetBindingDisplayString(bindingIndex), sprite);
		}

		private void SetDisplay(string text, Sprite sprite)
		{
			if (icon != null)
			{
				icon.sprite = sprite;
				icon.enabled = sprite != null;
			}
			bindingText.text = text;
			bindingText.enabled = sprite == null;
		}

		private void OnClick()
		{
			if (active == null)
			{
				StartCoroutine(RebindRoutine());
			}
		}

		private IEnumerator RebindRoutine()
		{
			active = this;
			SetDisplay(WAITING, null);

			// Wait for the submit press to end, or it would be captured as the new binding.
			yield return null;
			while (submitAction != null && submitAction.IsPressed())
			{
				yield return null;
			}

			string previousPath = action.bindings[bindingIndex].effectivePath;
			bool wasEnabled = action.enabled;
			action.Disable();

			operation = action.PerformInteractiveRebinding(bindingIndex)
				.WithBindingGroup(controlScheme)
				.WithControlsExcluding("<Pointer>/position")
				.WithControlsExcluding("<Pointer>/delta")
				.WithControlsExcluding("<Mouse>/scroll")
				.WithCancelingThrough("<Keyboard>/escape")
				.OnMatchWaitForAnother(0.1f)
				.OnCancel(_ => EndRebind(wasEnabled, null))
				.OnComplete(_ => EndRebind(wasEnabled, previousPath));

			// Only accept controls of the scheme being edited.
			if (controlScheme == ControlSchemes.GAMEPAD)
			{
				operation.WithControlsExcluding("<Keyboard>").WithControlsExcluding("<Mouse>").WithCancelingThrough("<Gamepad>/start");
			}
			else
			{
				operation.WithControlsExcluding("<Gamepad>");
			}

			operation.Start();
		}

		private void EndRebind(bool reenable, string previousPath)
		{
			operation?.Dispose();
			operation = null;
			active = null;
			blockUntil = Time.unscaledTime + INPUT_BLOCK_TIME;

			if (reenable)
			{
				action.Enable();
			}

			if (previousPath != null)
			{
				SwapDuplicate(previousPath);
				inputSettings.SaveBindings(Player, action.actionMap.asset);
			}
			Refresh();
		}

		/// <summary>
		/// Gives <paramref name="previousPath"/> to any other binding in the map that now shares our path.
		/// </summary>
		private void SwapDuplicate(string previousPath)
		{
			string path = action.bindings[bindingIndex].effectivePath;
			InputBinding mask = InputBinding.MaskByGroup(controlScheme);
			foreach (InputAction other in action.actionMap.actions)
			{
				for (int i = 0; i < other.bindings.Count; i++)
				{
					InputBinding binding = other.bindings[i];
					if ((other == action && i == bindingIndex) || binding.isComposite || !mask.Matches(binding))
					{
						continue;
					}
					if (binding.effectivePath == path)
					{
						other.ApplyBindingOverride(i, previousPath);
					}
				}
			}
		}

		private void CancelRebind()
		{
			if (operation != null)
			{
				operation.Cancel();
			}
			if (active == this)
			{
				active = null;
			}
		}
	}
}
