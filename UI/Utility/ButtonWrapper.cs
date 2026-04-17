using System;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Thin wrapper around a Button that adds a lock state while keeping the Button selectable.
	/// Intended for cases where the button must remain highlighted/selectable for controller navigation,
	/// but activation should be blocked.
	/// </summary>
	[RequireComponent(typeof(Button))]
	public class ButtonWrapper : MonoBehaviour
	{
		public event Action ClickedEvent;

		[SerializeField] private bool locked = false;

		[Header("Locked Visuals")]
		[SerializeField] private bool alterVisualsWhenLocked = true;
		[SerializeField] private CanvasGroup lockedVisualGroup;
		[SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.5f;
		[SerializeField] private bool tintTargetGraphicToDisabledColor = false;

		public Button Button
		{
			get
			{
				if (button == null)
				{
					button = GetComponent<Button>();
				}
				return button;
			}
		}
		private Button button;

		public bool Locked
		{
			get => locked;
			set
			{
				if (locked == value)
				{
					return;
				}

				locked = value;
				Refresh();
			}
		}

		private Graphic targetGraphic;
		private Color originalTargetGraphicColor;
		private bool hasCachedTargetGraphicColor = false;

		protected void Awake()
		{
			Button.onClick.AddListener(OnButtonClicked);

			CacheReferences();
			CacheOriginalVisualState();
			Refresh();
		}

		protected void OnEnable()
		{
			CacheReferences();
			CacheOriginalVisualState();
			Refresh();
		}

		protected void OnDestroy()
		{
			if (button != null)
			{
				button.onClick.RemoveListener(OnButtonClicked);
			}
		}

		private void CacheReferences()
		{
			if (lockedVisualGroup == null)
			{
				lockedVisualGroup = GetComponent<CanvasGroup>();
			}

			if (Button != null)
			{
				targetGraphic = Button.targetGraphic;
			}
		}

		private void CacheOriginalVisualState()
		{
			if (targetGraphic != null && !hasCachedTargetGraphicColor)
			{
				originalTargetGraphicColor = targetGraphic.color;
				hasCachedTargetGraphicColor = true;
			}
		}

		private void OnButtonClicked()
		{
			if (locked)
			{
				return;
			}

			ClickedEvent?.Invoke();
		}

		public void Refresh()
		{
			// Keep the underlying button interactable so it remains selectable and highlighted.
			Button.interactable = true;

			ApplyLockedVisuals();
		}

		private void ApplyLockedVisuals()
		{
			if (!alterVisualsWhenLocked)
			{
				RestoreVisuals();
				return;
			}

			if (locked)
			{
				if (lockedVisualGroup != null)
				{
					lockedVisualGroup.alpha = lockedAlpha;
				}

				if (tintTargetGraphicToDisabledColor && targetGraphic != null)
				{
					ColorBlock colors = Button.colors;
					targetGraphic.color = colors.disabledColor * colors.colorMultiplier;
				}
			}
			else
			{
				RestoreVisuals();
			}
		}

		private void RestoreVisuals()
		{
			if (lockedVisualGroup != null)
			{
				lockedVisualGroup.alpha = 1f;
			}

			if (targetGraphic != null && hasCachedTargetGraphicColor)
			{
				targetGraphic.color = originalTargetGraphicColor;
			}
		}

#if UNITY_EDITOR
		protected void OnValidate()
		{
			if (button == null)
			{
				button = GetComponent<Button>();
			}

			CacheReferences();
			CacheOriginalVisualState();
			Refresh();
		}
#endif
	}
}
