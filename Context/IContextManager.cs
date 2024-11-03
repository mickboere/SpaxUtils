using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for an object able to manage a context.
	/// </summary>
	public interface IContextManager
	{
		/// <summary>
		/// Invoked whenever any change has occured in the context stack.
		/// </summary>
		event Action ContextChangedEvent;

		/// <summary>
		/// Returns the full context stack.
		/// </summary>
		List<string> GetStack();

		/// <summary>
		/// Returns whether <paramref name="context"/> is present in the context stack and active.
		/// </summary>
		bool IsActive(string context);

		/// <summary>
		/// Mutes a context.
		/// </summary>
		void Mute(string context, bool mute);

		/// <summary>
		/// Solo's out a context.
		/// </summary>
		void Solo(string context, bool solo);

		/// <summary>
		/// Switch the root context, clearing all contexts pushed on top.
		/// </summary>
		void Switch(string context);

		/// <summary>
		/// Switch out the entire context stack, clearing all contexts not in <paramref name="contextStack"/>.
		/// </summary>
		void Switch(string[] contextStack);

		/// <summary>
		/// Push a new context layer on top of the current stack.
		/// </summary>
		void Push(string context);

		/// <summary>
		/// Pop a context layer from the stack, taking down all layers above it with it.
		/// </summary>
		void Pop(string context);
	}
}
