using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base implementation for <see cref="IInteractor"/>.
	/// </summary>
	public abstract class InteractorBase : EntityComponent, IInteractor
	{
		public InteractorBase(IEntity entity, IDependencyManager dependencyManager) : base(entity, dependencyManager)
		{
		}

		/// <inheritdoc/>
		public abstract List<string> GetInteractions(IInteractable interactable);

		/// <inheritdoc/>
		public abstract bool TryCreateInteraction(IInteractable interactable, string action, out IInteraction interaction);
	}
}
