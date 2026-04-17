using System;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.UI;

namespace SpaxUtils
{
	/// <summary>
	/// Global dialogue box service. Provides convenience methods for common dialogue patterns (confirm, confirm/cancel).
	/// The <see cref="DialogueBoxUI"/> registers itself with this service on injection.
	/// </summary>
	public class DialogueBoxService : IService
	{
		public const string DEFAULT_CONFIRM = "Confirm";
		public const string DEFAULT_CANCEL = "Cancel";

		private DialogueBoxUI instance;

		/// <summary>
		/// Called by <see cref="DialogueBoxUI"/> on injection to register itself.
		/// </summary>
		public void Register(DialogueBoxUI dialogueBoxUI)
		{
			instance = dialogueBoxUI;
		}

		/// <summary>
		/// Called by <see cref="DialogueBoxUI"/> on destroy to unregister itself.
		/// </summary>
		public void Unregister(DialogueBoxUI dialogueBoxUI)
		{
			if (instance == dialogueBoxUI)
			{
				instance = null;
			}
		}

		/// <summary>
		/// Show a dialogue box with fully custom options.
		/// </summary>
		public void Show(string title, Sprite image = null, string body = null,
			List<Option> options = null, bool addCancel = false, Action onCancel = null, string cancelText = DEFAULT_CANCEL,
			bool horizontal = false, bool pause = true)
		{
			if (options == null)
			{
				options = new List<Option>();
			}

			if (addCancel)
			{
				options.Add(new Option(cancelText, "", (o) => { onCancel?.Invoke(); }, InputActions.CANCEL));
			}

			if (options.Count == 0)
			{
				ShowConfirm(title, image, body, horizontal: horizontal, pause: pause);
				return;
			}

			if (!EnsureInstance())
			{
				return;
			}
			instance.Show(title, image, body, options, horizontal, pause);
		}

		/// <summary>
		/// Show a dialogue box with a single confirm button and optionally a cancel button.
		/// </summary>
		public void ShowConfirm(string title, Sprite image = null, string body = null,
			Action onConfirm = null, string confirmText = DEFAULT_CONFIRM,
			bool addCancel = false, Action onCancel = null, string cancelText = DEFAULT_CANCEL,
			bool horizontal = true, bool pause = true)
		{
			List<Option> options = new List<Option>();
			options.Add(new Option(confirmText, "", (o) => onConfirm?.Invoke()));

			if (addCancel)
			{
				options.Add(new Option(cancelText, "", (o) => { onCancel?.Invoke(); }, InputActions.CANCEL));
			}

			if (!EnsureInstance())
			{
				return;
			}
			instance.Show(title, image, body, options, horizontal, pause);
		}

		/// <summary>
		/// Show a dialogue box with confirm and cancel buttons.
		/// </summary>
		public void ShowConfirmCancel(string title, Sprite image = null, string body = null,
			Action onConfirm = null, string confirmText = DEFAULT_CONFIRM,
			Action onCancel = null, string cancelText = DEFAULT_CANCEL,
			bool horizontal = true, bool pause = true)
		{
			List<Option> options = new List<Option>();
			options.Add(new Option(confirmText, "", (o) => onConfirm?.Invoke()));
			options.Add(new Option(cancelText, "", (o) => { onCancel?.Invoke(); }, InputActions.CANCEL));

			if (!EnsureInstance())
			{
				return;
			}
			instance.Show(title, image, body, options, horizontal, pause);
		}

		/// <summary>
		/// Explicitly hide the current dialogue box.
		/// </summary>
		public void Hide()
		{
			if (instance == null)
			{
				return;
			}
			instance.Hide();
		}

		private bool EnsureInstance()
		{
			if (instance != null)
			{
				return true;
			}

			SpaxDebug.Error("No DialogueBoxUI registered.", "Ensure a DialogueBoxUI exists in the player UI hierarchy.");
			return false;
		}
	}
}
