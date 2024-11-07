using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Generic UI menu item containing just a button, image and label.
	/// </summary>
	public class MenuItem : MonoBehaviour
	{
		/// <summary>
		/// Invoked when the menu item button is clicked, sends the <see cref="ID"/>.
		/// </summary>
		public event Action<string> ButtonClickedEvent;

		public string ID { get; private set; }

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

		public virtual void SetData<T>(T data) { }

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
	}
}
