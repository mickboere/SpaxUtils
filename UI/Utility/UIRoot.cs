using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	public class UIRoot : MonoBehaviour, IDependency
	{
		public RectTransform CanvasRect
		{
			get
			{
				if (_canvasRect == null)
				{
					_canvasRect = Canvas.GetComponent<RectTransform>();
				}
				return _canvasRect;
			}
		}
		private RectTransform _canvasRect;

		[field: SerializeField] public Canvas Canvas { get; private set; }
		[field: SerializeField] public Camera Camera { get; private set; }
		[field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }
		[field: SerializeField] public UIGroup UIGroup { get; private set; }
	}
}
