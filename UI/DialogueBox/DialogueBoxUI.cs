using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Dialogue box UI component that manages the full lifecycle of showing, hiding,
	/// populating and chaining dialogue boxes. Handles input, cursor and pause services locally.
	/// Registers itself with <see cref="DialogueBoxManager"/> on injection.
	/// </summary>
	public class DialogueBoxUI : MonoBehaviour
	{
		/// <summary>
		/// Fired when the box has fully hidden with no pending chain.
		/// </summary>
		public event Action ClosedEvent;

		/// <summary>
		/// Fired when any option is picked, before auto-hide begins.
		/// </summary>
		public event Action<Option> OptionPickedEvent;

		[Header("Visuals")]
		[SerializeField] private UIGroup uiGroup;
		[SerializeField] private GameObject titleObject;
		[SerializeField] private TMP_Text titleText;
		[SerializeField] private GameObject bodyObject;
		[SerializeField] private TMP_Text bodyText;
		[SerializeField] private GameObject imageObject;
		[SerializeField] private Image imageDisplay;

		[Header("Options")]
		[SerializeField] private OptionsMenuUI horizontalOptionsMenu;
		[SerializeField] private OptionsMenuUI verticalOptionsMenu;

		[Header("Input")]
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string uiActionMap;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string navigationActionMap;

		private DialogueBoxService manager;
		private PlayerInputWrapper playerInputWrapper;
		private CursorService cursorService;
		private TimeService timeService;

		private enum BoxState { Hidden, Showing, Visible, Hiding }
		private BoxState state = BoxState.Hidden;
		private OptionsMenuUI activeOptionsMenu;
		private bool currentPause;
		private object inputSubscriber = new object();

		private struct BoxContent
		{
			public string Title;
			public string Body;
			public Sprite Image;
			public List<Option> Options;
			public bool Horizontal;
			public bool Pause;

			public BoxContent(string title, string body, Sprite image, List<Option> options, bool horizontal, bool pause)
			{
				Title = title;
				Body = body;
				Image = image;
				Options = options;
				Horizontal = horizontal;
				Pause = pause;
			}
		}

		private BoxContent? pending;

		public void InjectDependencies(DialogueBoxService manager, PlayerInputWrapper playerInputWrapper, CursorService cursorService, TimeService timeService)
		{
			this.manager = manager;
			this.playerInputWrapper = playerInputWrapper;
			this.cursorService = cursorService;
			this.timeService = timeService;

			manager.Register(this);
		}

		protected void Awake()
		{
			uiGroup.gameObject.SetActive(false);
			horizontalOptionsMenu.SelectedOptionEvent += OnOptionSelected;
			verticalOptionsMenu.SelectedOptionEvent += OnOptionSelected;
		}

		protected void OnDestroy()
		{
			horizontalOptionsMenu.SelectedOptionEvent -= OnOptionSelected;
			verticalOptionsMenu.SelectedOptionEvent -= OnOptionSelected;
			ReleaseServices();

			if (manager != null)
			{
				manager.Unregister(this);
			}
		}

		/// <summary>
		/// Show a dialogue box with the given content. If already showing or hiding,
		/// the new content is stored as pending and displayed after the current transition completes.
		/// </summary>
		public void Show(string title, Sprite image, string body, List<Option> options, bool horizontal, bool pause)
		{
			BoxContent content = new BoxContent(title, body, image, options, horizontal, pause);

			switch (state)
			{
				case BoxState.Hidden:
					uiGroup.gameObject.SetActive(true);
					Populate(content);
					state = BoxState.Showing;
					uiGroup.Show(OnShowComplete);
					break;

				case BoxState.Showing:
				case BoxState.Visible:
					pending = content;
					state = BoxState.Hiding;
					if (activeOptionsMenu != null)
					{
						activeOptionsMenu.DisableAllOptions();
					}
					uiGroup.Hide(OnHideComplete);
					break;

				case BoxState.Hiding:
					pending = content;
					break;
			}
		}

		/// <summary>
		/// Explicitly hide the dialogue box, clearing any pending chain.
		/// </summary>
		public void Hide()
		{
			pending = null;

			switch (state)
			{
				case BoxState.Hidden:
					break;

				case BoxState.Showing:
				case BoxState.Visible:
					state = BoxState.Hiding;
					if (activeOptionsMenu != null)
					{
						activeOptionsMenu.DisableAllOptions();
					}
					uiGroup.Hide(OnHideComplete);
					break;

				case BoxState.Hiding:
					// Already hiding; pending was cleared so it will close fully.
					break;
			}
		}

		private void Populate(BoxContent content)
		{
			// Title.
			bool hasTitle = !string.IsNullOrEmpty(content.Title);
			if (titleObject != null)
			{
				titleObject.SetActive(hasTitle);
			}
			if (titleText != null)
			{
				titleText.text = hasTitle ? content.Title : "";
			}

			// Body.
			bool hasBody = !string.IsNullOrEmpty(content.Body);
			if (bodyObject != null)
			{
				bodyObject.SetActive(hasBody);
			}
			if (bodyText != null)
			{
				bodyText.text = hasBody ? content.Body : "";
			}

			// Image.
			bool hasImage = content.Image != null;
			if (imageObject != null)
			{
				imageObject.SetActive(hasImage);
			}
			if (imageDisplay != null)
			{
				imageDisplay.sprite = content.Image;
			}

			// Options layout.
			activeOptionsMenu = content.Horizontal ? horizontalOptionsMenu : verticalOptionsMenu;
			horizontalOptionsMenu.gameObject.SetActive(content.Horizontal);
			verticalOptionsMenu.gameObject.SetActive(!content.Horizontal);

			// Populate options.
			activeOptionsMenu.Initialize(content.Options, null, false);

			// Restrict controller navigation to only the dialogue box buttons.
			RestrictNavigation(content.Horizontal);

			// Input, cursor, pause.
			UpdateServices(content.Options.Count, content.Pause);
		}

		private void RestrictNavigation(bool horizontal)
		{
			Selectable[] selectables = activeOptionsMenu.GetComponentsInChildren<Selectable>(false);
			for (int i = 0; i < selectables.Length; i++)
			{
				Navigation nav = new Navigation();
				nav.mode = Navigation.Mode.Explicit;

				if (horizontal)
				{
					nav.selectOnLeft = selectables[(i - 1 + selectables.Length) % selectables.Length];
					nav.selectOnRight = selectables[(i + 1) % selectables.Length];
				}
				else
				{
					nav.selectOnUp = selectables[(i - 1 + selectables.Length) % selectables.Length];
					nav.selectOnDown = selectables[(i + 1) % selectables.Length];
				}

				selectables[i].navigation = nav;
			}
		}

		private void UpdateServices(int optionCount, bool pause)
		{
			// Action maps: always UI, navigation only if more than 1 option.
			if (optionCount > 1)
			{
				playerInputWrapper.RequestActionMaps(inputSubscriber, 2, uiActionMap, navigationActionMap);
			}
			else
			{
				playerInputWrapper.RequestActionMaps(inputSubscriber, 2, uiActionMap);
			}

			// Cursor.
			if (playerInputWrapper.CurrentControlScheme == ControlSchemes.KEYBOARD_AND_MOUSE)
			{
				cursorService.RequestCursor(this);
			}

			// Pause.
			if (pause)
			{
				timeService.RequestPause(this);
			}
			else if (currentPause)
			{
				timeService.CompletePauseRequest(this);
			}
			currentPause = pause;
		}

		private void ReleaseServices()
		{
			playerInputWrapper.CompleteActionMapRequest(inputSubscriber);
			cursorService.CompleteRequest(this);
			if (currentPause)
			{
				timeService.CompletePauseRequest(this);
				currentPause = false;
			}
		}

		private void OnShowComplete()
		{
			state = BoxState.Visible;
		}

		private void OnHideComplete()
		{
			if (pending.HasValue)
			{
				BoxContent content = pending.Value;
				pending = null;
				Populate(content);
				state = BoxState.Showing;
				uiGroup.Show(OnShowComplete);
			}
			else
			{
				state = BoxState.Hidden;
				ReleaseServices();
				uiGroup.gameObject.SetActive(false);
				ClosedEvent?.Invoke();
			}
		}

		private void OnOptionSelected(Option option)
		{
			OptionPickedEvent?.Invoke(option);

			if (activeOptionsMenu != null)
			{
				activeOptionsMenu.DisableAllOptions();
			}

			state = BoxState.Hiding;
			uiGroup.Hide(OnHideComplete);
		}
	}
}
