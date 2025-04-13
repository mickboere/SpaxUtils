using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Generic UI menu item containing just a button, image and label.
	/// </summary>
	public class MenuItem : MonoBehaviour, ISelectHandler
	{
		/// <summary>
		/// Invoked when the menu item button is clicked, sends the <see cref="ID"/>.
		/// </summary>
		public event Action<string> ButtonClickedEvent;

		/// <summary>
		/// Invoked when the item button is highlighted, sends the <see cref="ID"/>.
		/// </summary>
		public event Action<string> ButtonHighlightEvent;

		public string ID { get; private set; }
		public object Data { get; private set; }

		public Button Button => button;
		public Image Image => image;
		public TMP_Text Label => label;

		public Sprite Sprite
		{
			get
			{
				return image != null ? image.sprite : null;
			}
			set
			{
				if (image != null)
				{
					image.sprite = value;
				}
			}
		}

		public string Text
		{
			get
			{
				return label != null ? label.text : null;
			}
			set
			{
				if (label != null)
				{
					label.text = value;
				}
			}
		}

		[SerializeField] protected Button button;
		[SerializeField] protected Image image;
		[SerializeField] protected TMP_Text label;

		public virtual void SetData(object data)
		{
			Data = data;
		}

		protected virtual void OnEnable()
		{
			button?.onClick.AddListener(OnButtonClicked);
		}

		protected virtual void OnDisable()
		{
			button?.onClick.RemoveListener(OnButtonClicked);
		}

		public void Visualize(string id, Sprite sprite, string text)
		{
			ID = id;

			Sprite = sprite;
			if (image != null)
			{
				image.gameObject.SetActive(sprite != null);
			}

			Text = text;
			if (label != null)
			{
				label.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
			}
		}

		private void OnButtonClicked()
		{
			ButtonClickedEvent?.Invoke(ID);
		}

		public void OnSelect(BaseEventData eventData)
		{
			ButtonHighlightEvent?.Invoke(ID);
		}
	}
}
