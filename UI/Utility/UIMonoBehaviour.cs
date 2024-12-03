using UnityEngine;

namespace SpaxUtils.UI
{
	public class UIMonoBehaviour : MonoBehaviour
	{
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

		public UIRoot Root { get; private set; }

		public void InjectDependencies([Optional] UIRoot uiRoot)
		{
			Root = uiRoot;
		}
	}
}
