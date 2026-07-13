using UnityEngine;
using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Service that allows other classes to request the cursor.
	/// </summary>
	public class CursorService : IService, IDisposable
	{
		public List<object> requests;

		private CallbackService callbackService;

		public CursorService(CallbackService callbackService)
		{
			this.callbackService = callbackService;

			requests = new List<object>();

			callbackService.ApplicationFocusCallback += OnApplicationFocus;

			RefreshCursorState();
		}

		public void Dispose()
		{
			if (callbackService != null)
			{
				callbackService.ApplicationFocusCallback -= OnApplicationFocus;
			}
		}

		public void LockCursor(object context, bool lockCursor)
		{
			if (lockCursor)
			{
				CompleteRequest(context);
			}
			else
			{
				RequestCursor(context);
			}
		}

		public void RequestCursor(object context)
		{
			if (!requests.Contains(context))
			{
				requests.Add(context);
			}

			RefreshCursorState();
		}

		public void CompleteRequest(object context)
		{
			if (requests.Contains(context))
			{
				requests.Remove(context);
			}

			RefreshCursorState();
		}

		/// <summary>
		/// Unity resets the cursor state when the application loses focus (or when ESC is pressed in a build), so we reapply it on regain.
		/// </summary>
		private void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus)
			{
				RefreshCursorState();
			}
		}

		private void RefreshCursorState()
		{
			LockCursor(requests.Count == 0);
		}

		private void LockCursor(bool value)
		{
			Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !value;
		}
	}
}
