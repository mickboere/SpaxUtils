using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation of an <see cref="IInteraction"/>.
	/// </summary>
	public class Interaction : IInteraction
	{
		/// <inheritdoc/>
		public event Action<IInteraction> InitiatedEvent;

		/// <inheritdoc/>
		public event Action<IInteraction> ConcludedEvent;

		/// <inheritdoc/>
		public IEntity Interactor { get; }

		/// <inheritdoc/>
		public IInteractable Interactable { get; }

		/// <inheritdoc/>
		public string Action { get; }

		/// <inheritdoc/>
		public object Data { get; set; }

		/// <inheritdoc/>
		public bool Initiated { get; private set; }

		/// <inheritdoc/>
		public bool Concluded { get; private set; }

		/// <inheritdoc/>
		public bool Success { get; private set; }

		/// <inheritdoc/>
		public string Message { get; private set; }

		public Interaction(IEntity interactor, IInteractable interactable, string action, object data = null)
		{
			Interactor = interactor;
			Interactable = interactable;
			Action = action;
			Data = data;
		}

		/// <inheritdoc/>
		public bool TryInitiate()
		{
			if (Initiated)
			{
				SpaxDebug.Error("Tried to initiate interaction but it was already initiated.");
				return false;
			}
			if (Concluded)
			{
				SpaxDebug.Error("Tried to initiate interaction but it was already concluded.");
				return false;
			}

			if (Interactable.TryInteract(this))
			{
				Initiated = true;
				InitiatedEvent?.Invoke(this);
				return true;
			}
			else
			{
				Conclude(false, "Interactable could not be interacted with."); // Later TODO: have interactable return failure reason for message popup.
				return false;
			}
		}

		/// <inheritdoc/>
		public void Conclude(bool success, string message = "")
		{
			if (Concluded)
			{
				SpaxDebug.Error($"Interaction was already concluded as a {(Success ? "Success" : "Failure")}!",
					$"Attempted to conclude interaction of type '{Action}' as a {(success ? "Success" : "Failure")} with message: \"{message}\" but it was already concluded with the following message: \"{Message}\"");
				return;
			}

			Concluded = true;
			Success = success;
			Message = message;
			ConcludedEvent?.Invoke(this);
		}

		public void Dispose()
		{
			if (!Concluded)
			{
				Conclude(false, "Interaction was disposed.");
			}
		}

		public override string ToString()
		{
			return $"Interaction(interactor={Interactor.Identification.Name}, interactable={Interactable.Entity.Identification.Name}, action={Action}, data={Data}, concluded={Concluded}, success={Success})";
		}
	}
}
