using UnityEngine;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Service that allows other classes to request the cursor.
	/// </summary>
	public class CursorService : IService
	{
		public List<object> requests;

		public CursorService()
		{
			requests = new List<object>();
			RefreshCursorState();
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
