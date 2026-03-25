using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaxUtils
{
	public class UIAudioEvents : MonoBehaviour,
		ISelectHandler,
		ISubmitHandler,
		IPointerClickHandler
	{
		public enum UIAudioElementType
		{
			Default,
			Confirm,
			Cancel
		}

		[SerializeField] private UIAudioElementType elementType = UIAudioElementType.Default;

		private UIAudioManager uiAudioService;
		private bool isDisposed;

		public void InjectDependencies(UIAudioManager uiAudioService)
		{
			this.uiAudioService = uiAudioService;
		}

		protected void OnEnable()
		{
			if (uiAudioService == null)
			{
				// Ensure we have service.
				uiAudioService = GlobalDependencyManager.Instance.Get<UIAudioManager>();
			}
		}

		protected void OnDestroy()
		{
			isDisposed = true;
			uiAudioService = null;
		}

		public void OnSelect(BaseEventData eventData)
		{
			if (isDisposed)
			{
				return;
			}

			uiAudioService.PlayNavigation();
		}

		public void OnSubmit(BaseEventData eventData)
		{
			if (isDisposed)
			{
				return;
			}

			PlayActivation();
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (isDisposed)
			{
				return;
			}

			PlayActivation();
		}

		public void PlayActivation()
		{
			if (isDisposed)
			{
				return;
			}

			switch (elementType)
			{
				case UIAudioElementType.Cancel:
					uiAudioService.PlayCancel();
					break;

				case UIAudioElementType.Confirm:
					uiAudioService.PlayConfirmation();
					break;

				default:
					uiAudioService.PlaySelection();
					break;
			}
		}

		public void PlayNavigation()
		{
			if (isDisposed)
			{
				return;
			}

			uiAudioService.PlayNavigation();
		}

		public void PlaySelection()
		{
			if (isDisposed)
			{
				return;
			}

			uiAudioService.PlaySelection();
		}

		public void PlayConfirmation()
		{
			if (isDisposed)
			{
				return;
			}

			uiAudioService.PlayConfirmation();
		}

		public void PlayCancel()
		{
			if (isDisposed)
			{
				return;
			}

			uiAudioService.PlayCancel();
		}
	}
}
