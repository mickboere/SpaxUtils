using System;
using UnityEngine;
using UnityEngine.UI;
using SpaxUtils;

namespace SpaxUtils.UI
{
	/// <summary>
	/// CanvasGroup wrapper component component that manages its own transitions.
	/// </summary>
	[RequireComponent(typeof(CanvasGroup))]
	public class UIGroup : MonoBehaviour
	{
		private const string TT_FS = "Can also be a root object as first child selectable will be selected.";

		public RectTransform RectTransform
		{
			get
			{
				if (_rectTransform == null)
				{
					_rectTransform = GetComponent<RectTransform>();
				}
				return _rectTransform;
			}
		}
		private RectTransform _rectTransform;
		public CanvasGroup CanvasGroup
		{
			get
			{
				if (_canvasGroup == null)
				{
					_canvasGroup = GetComponent<CanvasGroup>();
				}
				return _canvasGroup;
			}
		}
		private CanvasGroup _canvasGroup;
		public TransitionSettings TransitionSettings => transitionSettings;
		public Selectable FirstSelectable => firstSelectable == null ? null : firstSelectable.GetComponentInChildren<Selectable>();

		public CanvasGroupTransitionHelper Transition
		{
			get
			{
				// Transition is created on demand because it can be requested before the object is ever enabled, meaning we can't wait until Awake().
				if (_transition == null)
				{
					_transition = new CanvasGroupTransitionHelper(CanvasGroup, transitionSettings);
					_transition.FilledEvent += OnFilled;
					_transition.EmptiedEvent += OnEmptied;
				}
				return _transition;
			}
		}
		private CanvasGroupTransitionHelper _transition;

		[Header("Group")]
		[SerializeField] protected TransitionSettings transitionSettings;
		[SerializeField, Tooltip(TT_FS)] protected GameObject firstSelectable;
		[SerializeField] private UIGroup[] children;

		protected virtual void OnDestroy()
		{
			Transition.Dispose();
		}

		protected virtual void Update()
		{
			Transition.TryUpdateProgress();
		}

		public virtual void ShowImmediately()
		{
			Transition.FillImmediately();
			foreach (UIGroup child in children)
			{
				child.ShowImmediately();
			}
			SelectFirstSelectable();
		}

		public virtual void HideImmediately()
		{
			Transition.EmptyImmediately();
			foreach (UIGroup child in children)
			{
				child.HideImmediately();
			}
		}

		public virtual void Show(Action callback = null, float delay = 0f)
		{
			Transition.Fill(() =>
			{
				SelectFirstSelectable();
				callback?.Invoke();
			}, delay);

			foreach (UIGroup child in children)
			{
				child.Show(null, delay + TransitionSettings.InTime * Transition.Progress.Invert());
			}
		}

		public virtual void Hide(Action callback = null, float delay = 0f)
		{
			Transition.Empty(() =>
			{
				foreach (UIGroup child in children)
				{
					child.HideImmediately();
				}
				callback?.Invoke();
			}, delay);
		}

		public virtual void SelectFirstSelectable()
		{
			FirstSelectable?.Select();
		}

		protected virtual void OnFilled()
		{
		}

		protected virtual void OnEmptied()
		{
		}
	}
}
