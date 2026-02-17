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
	public class UIGroup : UIMonoBehaviour
	{
		private const string TT_FS = "Can also be a root object as first child selectable will be selected.";

		public event Action ShowEvent;
		public event Action HideEvent;
		public event Action FilledEvent;
		public event Action EmptiedEvent;

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
			SelectFirstSelectable();
		}

		public virtual void HideImmediately()
		{
			Transition.EmptyImmediately();
		}

		public virtual void Show(Action callback = null, float delay = 0f, float overrideTime = -1f)
		{
			OnShow();
			Transition.Fill(() =>
			{
				SelectFirstSelectable();
				callback?.Invoke();
			}, delay, overrideTime);
		}

		public virtual void Hide(Action callback = null, float delay = 0f, float overrideTime = -1f)
		{
			OnHide();
			Transition.Empty(() =>
			{
				callback?.Invoke();
			}, delay, overrideTime);
		}

		public virtual void SelectFirstSelectable()
		{
			FirstSelectable?.Select();
		}

		protected virtual void OnShow()
		{
			ShowEvent?.Invoke();
		}

		protected virtual void OnHide()
		{
			HideEvent?.Invoke();
		}

		protected virtual void OnFilled()
		{
			FilledEvent?.Invoke();
		}

		protected virtual void OnEmptied()
		{
			EmptiedEvent?.Invoke();
		}
	}
}
