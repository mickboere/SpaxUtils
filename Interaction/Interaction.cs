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
		public event Action<IInteraction, bool> ConcludedEvent;

		/// <inheritdoc/>
		public string Type { get; }

		/// <inheritdoc/>
		public IInteractor Interactor { get; }

		/// <inheritdoc/>
		public IInteractable Interactable { get; }

		/// <inheritdoc/>
		public object Data { get; set; }

		/// <inheritdoc/>
		public bool Concluded
		{
			get { return _concluded; }
			set
			{
				SpaxDebug.Error("Set Interaction.Concluded", this.ToString());
				_concluded = value;
			}
		}
		private bool _concluded;

		/// <inheritdoc/>
		public bool Success { get; private set; }

		private Action<Interaction, bool> onConcluded;

		public Interaction(string type, IInteractor interactor, IInteractable interactable,
			object data = null, Action<IInteraction, bool> onConcluded = null)
		{
			Type = type;
			Interactor = interactor;
			Interactable = interactable;
			Data = data;
			this.onConcluded = onConcluded;
		}

		/// <inheritdoc/>
		public void Conclude(bool success)
		{
			if (Concluded)
			{
				SpaxDebug.Error($"Already concluded as a {(success ? "Success" : "Failure")}",
					$"Attempted to conclude interaction of type '{Type}' as a {(success ? "Success" : "Failure")} but it was already concluded.");
				return;
			}

			Concluded = true;
			Success = success;
			onConcluded?.Invoke(this, success);
			ConcludedEvent?.Invoke(this, success);
		}

		public void Dispose()
		{
			if (!Concluded)
			{
				Conclude(false);
			}
		}

		public override string ToString()
		{
			return $"Interaction(type={Type}, interactor={Interactor.Entity.Identification.Name}, interactable={Interactable.Entity.Identification.Name}, data={Data}, concluded={Concluded}, success={Success})";
		}
	}
}
