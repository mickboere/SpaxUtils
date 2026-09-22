using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputAction;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Settings screen built at runtime from the <see cref="SettingsService"/>: tabs, sections, one row per setting.
	/// Joined players (index above 0) only see their own per-player settings.
	/// </summary>
	public class SettingsScreen : UIScreen
	{
		// How long a closed dropdown or dialogue keeps eating Back, since its own cancel fires on the same press.
		private const float CLOSE_GRACE = 0.15f;

		[Header("Settings Screen")]
		[SerializeField] private RectTransform tabParent;
		[SerializeField] private RectTransform rowParent;
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers))] private string backContext;
		[SerializeField, Tooltip("Tabs listed here come first, in this order; any others follow in discovery order.")]
		private string[] tabOrder = { "Video", "Graphics", "Audio", "Gameplay", "Input", "Multiplayer" };
		[SerializeField, Tooltip("Seconds before an unconfirmed display change reverts.")]
		private float confirmTimeout = 15f;

		[Header("Prefabs")]
		[SerializeField] private SettingsTabUI tabButton;
		[SerializeField] private TMP_Text sectionHeader;
		[SerializeField] private SliderSettingRowUI sliderRow;
		[SerializeField] private ToggleSettingRowUI toggleRow;
		[SerializeField] private DropdownSettingRowUI dropdownRow;
		[SerializeField] private KeybindRowUI keybindRow;

		[Header("Rebinding")]
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string[] rebindableMaps;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private string[] excludedActions;

		[Header("Input")]
		[SerializeField, ConstDropdown(typeof(IInputActions))] private string backAction;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private string resetAction;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private string previousTabAction;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private string nextTabAction;
		[SerializeField, ConstDropdown(typeof(IInputActions))]
		[Tooltip("Held while starting a rebind; waited out so it isn't captured.")]
		private string submitAction;

		private SettingsService settingsService;
		private UIScreenManager screenManager;
		private PlayerInputWrapper playerInputWrapper;
		private PlayerInputService playerInputService;
		private RuntimeDataService runtimeDataService;
		private PlayerInputSettings playerInputSettings;
		private InputIconLibrary inputIconLibrary;
		private DialogueBoxService dialogueBoxService;
		private EventSystem eventSystem;

		private readonly List<string> tabs = new List<string>();
		private readonly List<Setting> visible = new List<Setting>();
		private readonly List<SettingsTabUI> tabButtons = new List<SettingsTabUI>();
		private readonly List<SettingRowUI> rows = new List<SettingRowUI>();
		private readonly List<GameObject> spawned = new List<GameObject>();
		private readonly List<Option> options = new List<Option>();
		private int currentTab;
		private int player;
		private bool open;
		private float lastModalTime;
		private Action pendingRevert;
		private float revertDeadline;

		public void InjectDependencies(SettingsService settingsService, UIScreenManager screenManager,
			PlayerInputWrapper playerInputWrapper, PlayerInputService playerInputService, RuntimeDataService runtimeDataService,
			PlayerInputSettings playerInputSettings, InputIconLibrary inputIconLibrary, DialogueBoxService dialogueBoxService,
			[Optional] EventSystem eventSystem)
		{
			this.settingsService = settingsService;
			this.screenManager = screenManager;
			this.playerInputWrapper = playerInputWrapper;
			this.playerInputService = playerInputService;
			this.runtimeDataService = runtimeDataService;
			this.playerInputSettings = playerInputSettings;
			this.inputIconLibrary = inputIconLibrary;
			this.dialogueBoxService = dialogueBoxService;
			this.eventSystem = eventSystem;

			options.Add(CreateOption("Back", backAction, OnBack));
			options.Add(CreateOption("Reset", resetAction, OnReset));
			options.Add(CreateOption("Previous Tab", previousTabAction, () => StepTab(-1)));
			options.Add(CreateOption("Next Tab", nextTabAction, () => StepTab(1)));
		}

		protected override void OnDestroy()
		{
			Close();
			foreach (Option option in options)
			{
				option.Dispose();
			}
			base.OnDestroy();
		}

		protected override void Update()
		{
			base.Update();
			if (pendingRevert != null || rows.Any(r => r is DropdownSettingRowUI d && d.IsExpanded))
			{
				lastModalTime = Time.unscaledTime;
			}

			if (pendingRevert != null && Time.unscaledTime >= revertDeadline)
			{
				dialogueBoxService.Hide();
				ResolvePending(true);
			}
		}

		public override void SelectFirstSelectable()
		{
			Select(rows.Count > 0 ? rows[0].Selectable : null);
		}

		protected override void OnShow()
		{
			Open();
			base.OnShow();
		}

		protected override void OnHide()
		{
			Close();
			base.OnHide();
		}

		private void Open()
		{
			if (open || settingsService == null)
			{
				return;
			}
			open = true;

			player = Mathf.Max(0, playerInputService.GetPlayerIndex(playerInputWrapper));
			visible.Clear();
			visible.AddRange(settingsService.Settings.Where(IsVisible));

			tabs.Clear();
			tabs.AddRange(tabOrder.Where(t => visible.Any(s => s.Info.Tab == t)));
			tabs.AddRange(visible.Select(s => s.Info.Tab).Distinct().Where(t => !tabs.Contains(t)));
			ToggleGroup tabGroup = tabParent.GetComponent<ToggleGroup>();
			if (tabGroup == null)
			{
				tabGroup = tabParent.gameObject.AddComponent<ToggleGroup>();
			}
			tabGroup.allowSwitchOff = false;
			for (int i = 0; i < tabs.Count; i++)
			{
				int index = i;
				SettingsTabUI tab = Instantiate(tabButton, tabParent);
				tab.Setup(tabs[i], tabGroup, () => ShowTab(index));
				tabButtons.Add(tab);
			}

			foreach (Option option in options)
			{
				option.Enable(playerInputWrapper);
			}
			playerInputWrapper.ControlSchemeChangedEvent += OnControlSchemeChanged;

			ShowTab(Mathf.Clamp(currentTab, 0, Mathf.Max(0, tabs.Count - 1)));
		}

		private void Close()
		{
			if (!open)
			{
				return;
			}
			open = false;

			// Leaving without confirming counts as declining.
			if (pendingRevert != null)
			{
				dialogueBoxService.Hide();
				ResolvePending(true);
			}

			foreach (Option option in options)
			{
				option.Disable();
			}
			playerInputWrapper.ControlSchemeChangedEvent -= OnControlSchemeChanged;

			ClearRows();
			foreach (SettingsTabUI tab in tabButtons)
			{
				Destroy(tab.gameObject);
			}
			tabButtons.Clear();
			settingsService.Save();
		}

		private bool IsVisible(Setting setting)
		{
			if (player > 0 && setting.Scope != SettingScope.Player)
			{
				return false;
			}
			if (setting.Scope == SettingScope.Profile && runtimeDataService.CurrentProfile == null)
			{
				return false;
			}
			return setting is BindingsSetting ? keybindRow != null : GetRowPrefab(setting) != null;
		}

		#region Building

		/// <summary>
		/// The row prefab presenting <paramref name="setting"/>, or null when it has no single-row form.
		/// </summary>
		private SettingRowUI GetRowPrefab(Setting setting)
		{
			return setting switch
			{
				BindingsSetting => null,
				BoolSetting => toggleRow,
				IOptionSetting => dropdownRow,
				FloatSetting or IntSetting => sliderRow,
				_ => null
			};
		}

		private void ShowTab(int index)
		{
			if (index < 0 || index >= tabs.Count)
			{
				return;
			}

			currentTab = index;
			// The toggle group switches the previous tab off.
			tabButtons[index].SetSelected(true);

			ClearRows();
			// Grouped, since different suppliers may add to the same section.
			foreach (IGrouping<string, Setting> section in visible.Where(s => s.Info.Tab == tabs[index]).GroupBy(s => s.Info.Section))
			{
				AddSectionHeader(section.Key);
				foreach (Setting setting in section)
				{
					if (setting is BindingsSetting bindings)
					{
						AddKeybindRows(bindings);
					}
					else
					{
						SettingRowUI row = Instantiate(GetRowPrefab(setting), rowParent);
						row.ConfirmHandler = Confirm;
						row.Bind(setting, player);
						AddRow(row);
					}
				}
			}

			LinkNavigation();
			SelectFirstSelectable();
		}

		private void AddSectionHeader(string section)
		{
			if (sectionHeader != null && !string.IsNullOrEmpty(section))
			{
				TMP_Text header = Instantiate(sectionHeader, rowParent);
				header.text = section;
				spawned.Add(header.gameObject);
			}
		}

		private void AddKeybindRows(BindingsSetting setting)
		{
			InputActionAsset actions = playerInputWrapper.PlayerInput.actions;
			string scheme = playerInputWrapper.CurrentControlScheme;
			InputBinding mask = InputBinding.MaskByGroup(scheme);
			InputAction submit = string.IsNullOrEmpty(submitAction) ? null : playerInputWrapper.GetAction(submitAction);

			foreach (string mapName in rebindableMaps)
			{
				InputActionMap map = actions.FindActionMap(mapName);
				if (map == null)
				{
					continue;
				}

				foreach (InputAction action in map.actions)
				{
					if (excludedActions.Contains(action.name))
					{
						continue;
					}

					for (int i = 0; i < action.bindings.Count; i++)
					{
						InputBinding binding = action.bindings[i];
						// Skip composite heads and whole-stick/pointer bindings; only buttons and composite parts rebind.
						if (binding.isComposite || !mask.Matches(binding) ||
							(!binding.isPartOfComposite && action.expectedControlType == "Vector2"))
						{
							continue;
						}

						KeybindRowUI row = Instantiate(keybindRow, rowParent);
						row.Bind(setting, player, action, i, scheme, submit, playerInputSettings, inputIconLibrary);
						AddRow(row);
					}
				}
			}
		}

		private void AddRow(SettingRowUI row)
		{
			rows.Add(row);
			spawned.Add(row.gameObject);
		}

		private void LinkNavigation()
		{
			for (int i = 0; i < rows.Count; i++)
			{
				Navigation navigation = new Navigation
				{
					mode = Navigation.Mode.Explicit,
					selectOnUp = i > 0 ? rows[i - 1].Selectable : null,
					selectOnDown = i < rows.Count - 1 ? rows[i + 1].Selectable : null
				};
				rows[i].Selectable.navigation = navigation;
			}
		}

		private void ClearRows()
		{
			foreach (GameObject go in spawned)
			{
				Destroy(go);
			}
			spawned.Clear();
			rows.Clear();
		}

		#endregion Building

		#region Commands

		private Option CreateOption(string title, string action, Action callback)
		{
			return new Option(title, action, (CallbackContext context) =>
			{
				if (context.performed && open && !KeybindRowUI.BlockingInput)
				{
					callback();
				}
			}, playerInputWrapper, eatInput: true, prio: 10);
		}

		private void OnBack()
		{
			if (Time.unscaledTime - lastModalTime > CLOSE_GRACE)
			{
				screenManager.SwitchContext(backContext);
			}
		}

		private void OnReset()
		{
			if (eventSystem == null)
			{
				return;
			}

			GameObject selected = eventSystem.currentSelectedGameObject;
			SettingRowUI row = rows.FirstOrDefault(r => r.Selectable.gameObject == selected);
			row?.ResetToDefault();
		}

		private void StepTab(int step)
		{
			if (tabs.Count > 0)
			{
				ShowTab((currentTab + step + tabs.Count) % tabs.Count);
			}
		}

		private void Confirm(SettingRowUI row, Action change)
		{
			// Settle a pending change first, so every revert has one clear snapshot.
			if (pendingRevert != null)
			{
				dialogueBoxService.Hide();
				ResolvePending(false);
			}

			object previous = row.Setting.GetBoxed(row.Player);
			change();
			if (Equals(previous, row.Setting.GetBoxed(row.Player)))
			{
				return;
			}

			float timeout = confirmTimeout;
			pendingRevert = () => row.Setting.SetBoxed(previous, row.Player);
			revertDeadline = Time.unscaledTime + timeout;

			// The dialogue handles its own input; ours would eat its cancel.
			ToggleOptions(false);
			dialogueBoxService.ShowConfirmCancel("Keep these settings?", null, $"Reverting in {timeout:0} seconds.",
				() => ResolvePending(false, row), "Keep",
				() => ResolvePending(true, row), "Revert",
				pause: false);
		}

		private void ResolvePending(bool revert, SettingRowUI row = null)
		{
			Action revertAction = pendingRevert;
			pendingRevert = null;
			if (revert)
			{
				revertAction?.Invoke();
			}

			if (open)
			{
				ToggleOptions(true);
				Select(row != null ? row.Selectable : null);
			}
		}

		private void ToggleOptions(bool enable)
		{
			foreach (Option option in options)
			{
				if (enable)
				{
					option.Enable(playerInputWrapper);
				}
				else
				{
					option.Disable();
				}
			}
		}

		#endregion Commands

		private void OnControlSchemeChanged(string scheme)
		{
			// Keybind rows only list the active scheme's bindings.
			if (open && tabs.Count > 0 && visible.Any(s => s is BindingsSetting && s.Info.Tab == tabs[currentTab]))
			{
				ShowTab(currentTab);
			}
		}

		private void Select(Selectable selectable)
		{
			if (selectable == null)
			{
				return;
			}
			if (eventSystem != null)
			{
				eventSystem.SetSelectedGameObject(selectable.gameObject);
			}
			else
			{
				selectable.Select();
			}
		}
	}
}
