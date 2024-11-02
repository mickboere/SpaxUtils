using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	public class ContextManager : IContextManager
	{
		public event Action ContextChangedEvent;

		private IContextManager parent;
		private List<string> contextStack = new List<string>();
		private Dictionary<string, IContext> contexts = new Dictionary<string, IContext>();
		private bool hasSolo;
		private bool blockEvents;

		public ContextManager()
		{
		}

		public ContextManager(IContextManager parent)
		{
			this.parent = parent;
		}

		/// <inheritdoc/>
		public bool IsActive(string context)
		{
			return (parent != null && parent.IsActive(context)) ||
				(contexts.ContainsKey(context) && (contexts[context].Solo || (!hasSolo && !contexts[context].Mute)));
		}

		/// <inheritdoc/>
		public void Mute(string context, bool mute)
		{
			contexts[context].Mute = mute;
			OnChange();
		}

		/// <inheritdoc/>
		public void Solo(string context, bool solo)
		{
			contexts[context].Solo = solo;
			hasSolo = contexts.Values.Any(c => c.Solo);
			OnChange();
		}

		#region Stack

		/// <inheritdoc/>
		public void Switch(string context)
		{
			contextStack.Clear();
			contexts.Clear();
			Push(context);
		}

		/// <inheritdoc/>
		public void Switch(string[] context)
		{
			blockEvents = true;

			// First detect until where the stack matches.
			int match = -1;
			for (int i = 0; i < contextStack.Count && i < context.Length; i++)
			{
				if (contextStack[i] == context[i])
				{
					match = i;
				}
				else
				{
					break;
				}
			}

			// Pop all the contexts that don't match.
			if (match < 0)
			{
				Switch(context[0]);
				match = 0;
			}
			else if (match < contextStack.Count - 1)
			{
				Pop(contextStack[match + 1]);
			}

			// Push the new contexts on top.
			if (match < context.Length - 1)
			{
				for (int i = match + 1; i < context.Length; i++)
				{
					Push(context[i]);
				}
			}

			blockEvents = false;
			OnChange();
		}

		/// <inheritdoc/>
		public void Push(string context)
		{
			if (contextStack.Contains(context))
			{
				SpaxDebug.Error("Context stack already contains context:", context);
				return;
			}

			contextStack.Add(context);
			contexts.Add(context, new Context(context));
			OnChange();
		}

		/// <inheritdoc/>
		public void Pop(string context)
		{
			if (!contextStack.Contains(context))
			{
				SpaxDebug.Error("Context stack does not contain context:", context);
				return;
			}

			// Keep popping contexts until we encounter the desired one.
			for (int i = contextStack.Count - 1; i > 0; i--)
			{
				string c = contextStack[i];
				contextStack.RemoveAt(i);
				contexts.Remove(c);
				if (c == context)
				{
					break;
				}
			}

			OnChange();
		}

		#endregion Stack

		private void OnChange()
		{
			if (!blockEvents)
			{
				ContextChangedEvent?.Invoke();
			}
		}
	}
}
