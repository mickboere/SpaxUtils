using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

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

		[SerializeField] private MenuItem menuItemTemplate;
		[SerializeField] private TMP_Text menuTitle;

		private GameService gameService;
		private PlayerInputWrapper playerInputWrapper;
		private ICommunicationChannel comms;

		private ItemMenu<Option> _menu;
		private List<Option> menuOptions = new List<Option>();

		// When Initialize() is called while inactive (common in your flow), we defer selection until enabled/fill.
		private bool pendingEnsureSelection;

		// Minimal "next-frame reselect" to survive Unity clearing selection after Destroy().
		private Coroutine reselectionCoroutine;

		public void InjectDependencies(GameService gameService, PlayerInputWrapper playerInputWrapper, ICommunicationChannel comms)
		{
			this.gameService = gameService;
			this.playerInputWrapper = playerInputWrapper;
			this.comms = comms;
		}

		protected void OnEnable()
		{
			if (UIGroup != null)
			{
				UIGroup.FilledEvent += OnUIGroupFilled;
			}

			if (pendingEnsureSelection)
			{
				pendingEnsureSelection = false;
				EnsureValidSelection();
			}
		}

		protected void OnDisable()
		{
			if (UIGroup != null)
			{
				UIGroup.FilledEvent -= OnUIGroupFilled;
			}

			if (reselectionCoroutine != null)
			{
				StopCoroutine(reselectionCoroutine);
				reselectionCoroutine = null;
			}
		}

		protected void OnDestroy()
		{
			if (_menu != null)
			{
				_menu.Dispose();
			}
		}

		private void OnUIGroupFilled()
		{
			// When transitions toggle interactable state, selection can be lost; re-assert once fully visible.
			EnsureValidSelection();
		}

		public void Initialize(string context, string title, bool addCancel = false)
		{
			var request = RequestOptionsMsg<OptionsMenuUI>.New(this, context, comms);
			Initialize(request.Options, title, addCancel);
		}

		public void Initialize(IEnumerable<Option> options, string title, bool addCancel = false)
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

			EnsureValidSelection();
		}

		public void SelectCurrentOption()
		{
			foreach (KeyValuePair<string, (Option data, MenuItem visual)> item in Menu.Items)
			{
				if (gameService != null &&
					gameService.EventSystem != null &&
					gameService.EventSystem.currentSelectedGameObject == item.Value.visual.Button.gameObject)
				{
					item.Value.visual.Button.onClick.Invoke();
					return;
				}
			}
		}

		public void DisableAllOptions()
		{
			foreach (Option option in menuOptions)
			{
				option.Disable();
			}
		}

		private void CleanMenu()
		{
			// Unsubscribe.
			foreach (Option option in menuOptions)
			{
				option.PickedEvent -= OnPickedOption;
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
			SelectedOptionEvent?.Invoke(option);
		}

		private void EnsureValidSelection()
		{
			// If this menu is inactive in the hierarchy, selection can't be applied reliably yet.
			if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
			{
				pendingEnsureSelection = true;
				return;
			}

			ForceSelectFirstIfNeeded();

			// Unity can clear selection later in the frame after Destroy() of the previously-selected button.
			// Re-assert on the next frame to guarantee a selected element remains.
			if (reselectionCoroutine != null)
			{
				StopCoroutine(reselectionCoroutine);
			}
			reselectionCoroutine = StartCoroutine(CoReselectNextFrame());
		}

		private IEnumerator CoReselectNextFrame()
		{
			yield return null; // (swap to WaitForEndOfFrame() if still flaky)
			reselectionCoroutine = null;
			ForceSelectFirstIfNeeded();
		}

		private void ForceSelectFirstIfNeeded()
		{
			// If no EventSystem reference, fall back to UIGroup helper.
			if (gameService == null || gameService.EventSystem == null)
			{
				UIGroup.SelectFirstSelectable();
				return;
			}

			GameObject currentSelected = gameService.EventSystem.currentSelectedGameObject;

			// If current selection is already inside this menu, keep it.
			if (currentSelected != null && currentSelected.transform.IsChildOf(transform))
			{
				return;
			}

			// Prefer configured FirstSelectable.
			Selectable first = UIGroup.FirstSelectable;
			if (first == null || !first.gameObject.activeInHierarchy || !first.IsInteractable())
			{
				first = GetComponentsInChildren<Selectable>(true)
					.FirstOrDefault(s => s != null && s.gameObject.activeInHierarchy && s.IsInteractable());
			}

			if (first == null)
			{
				return;
			}

			gameService.EventSystem.SetSelectedGameObject(null);
			gameService.EventSystem.SetSelectedGameObject(first.gameObject);
			first.Select();
		}
	}
}
