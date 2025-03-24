using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract entity component with a base implementation for <see cref="IInteractor"/>.
	/// </summary>
	public abstract class InteractorComponentBase : EntityComponentMono, IInteractor
	{
		/// <inheritdoc/>
		public abstract List<string> GetInteractions(IInteractable interactable);

		/// <inheritdoc/>
		public abstract bool TryCreateInteraction(IInteractable interactable, string action, out IInteraction interaction);
	}
}
