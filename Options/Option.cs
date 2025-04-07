using System;
using UnityEngine;
using static UnityEngine.InputSystem.InputAction;

namespace SpaxUtils
{
	/// <summary>
	/// Object that can be picked as an option and/or pass input when made available.
	/// </summary>
	/// <seealso cref="RequestOptionsMsg{T}"/>
	public class Option : IDisposable
	{
		public event Action<Option> PickedEvent;
		public event Action<CallbackContext> ReceivedInputEvent;
		public event Action<Option> ActivatedEvent;
		public event Action<Option> DeactivatedEvent;

		/// <summary>
		/// Whether this option is currently able to be picked.
		/// </summary>
		public bool Enabled { get; private set; }

		public string Title { get; }
		public string Description { get; }
		public string InputAction { get; }
		public bool HasInputAction => !string.IsNullOrEmpty(InputAction);

		/// <summary>
		/// Whether this option is currently listening for input.
		/// </summary>
		public bool Listening { get; private set; }

		private readonly bool pickOnInput;
		private readonly bool eatInput;
		private readonly int prio;
		private readonly Action<Option> onPickedCallback;
		private readonly Action<CallbackContext> onInputCallback;

		private PlayerInputWrapper playerInputWrapper;

		public Option(
			string title,
			string description,
			Action<Option> onPickedCallback,
			string inputAction = null,
			bool pickOnInput = true,
			bool eatInput = false,
			int prio = 0,
			Action<CallbackContext> onInputCallback = null,
			PlayerInputWrapper playerInputWrapper = null)
		{
			Title = title;
			Description = description;
			this.onPickedCallback = onPickedCallback;
			InputAction = inputAction;
			this.pickOnInput = pickOnInput;
			this.eatInput = eatInput;
			this.prio = prio;
			this.onInputCallback = onInputCallback;
			this.playerInputWrapper = playerInputWrapper;
		}

		/// <summary>
		/// Create a new input-callback-only option.
		/// </summary>
		public Option(
			string title,
			string inputAction,
			Action<CallbackContext> onInputCallback,
			PlayerInputWrapper playerInputWrapper,
			bool eatInput = false,
			int prio = 0,
			bool enable = false)
		{
			Title = title;
			InputAction = inputAction;
			this.onInputCallback = onInputCallback;
			this.playerInputWrapper = playerInputWrapper;
			this.eatInput = eatInput;
			this.prio = prio;

			Description = "";
			onPickedCallback = null;
			pickOnInput = false;

			if (enable)
			{
				Enable();
			}
		}

		public void Dispose()
		{
			StopListening();
		}

		/// <summary>
		/// Enables this option to allow it to start listening for input while the context is active.
		/// </summary>
		public void Enable(PlayerInputWrapper playerInputWrapper = null)
		{
			Enabled = true;

			if (playerInputWrapper != null)
			{
				this.playerInputWrapper = playerInputWrapper;
			}

			if (!Listening)
			{
				StartListening();
			}
		}

		/// <summary>
		/// Disables this option to make it unable to be picked.
		/// </summary>
		public void Disable()
		{
			Enabled = false;

			if (Listening)
			{
				StopListening();
			}
		}

		/// <summary>
		/// Enables or disables this option depending on <paramref name="toggle"/>.
		/// </summary>
		public void Toggle(bool toggle)
		{
			if (toggle)
			{
				Enable();
			}
			else
			{
				Disable();
			}
		}

		/// <summary>
		/// Pick this option and invoke its callback.
		/// </summary>
		public void Pick()
		{
			if (!Enabled)
			{
				SpaxDebug.Log("Option cannot be picked because it is disabled.", ToString(), color: Color.red);
				return;
			}

			PickedEvent?.Invoke(this);
			onPickedCallback?.Invoke(this);
		}

		private void StartListening()
		{
			if (Listening || !HasInputAction)
			{
				return;
			}

			if (this.playerInputWrapper == null)
			{
				SpaxDebug.Error("Option could not be activated: has an input action but no PlayerInputWrapper.", ToString());
				return;
			}

			this.playerInputWrapper.Subscribe(this, InputAction,
				delegate (CallbackContext inputContext)
				{
					//SpaxDebug.Log("OnInput", ToString());

					onInputCallback?.Invoke(inputContext);
					ReceivedInputEvent?.Invoke(inputContext);

					if (pickOnInput && inputContext.canceled)
					{
						Pick();
					}

					return eatInput;
				},
				prio);

			Listening = true;
			ActivatedEvent?.Invoke(this);
		}

		private void StopListening()
		{
			if (!Listening)
			{
				return;
			}

			playerInputWrapper.Unsubscribe(this);
			Listening = false;
			DeactivatedEvent?.Invoke(this);
		}

		public override string ToString()
		{
			return $"Option\n{{\n\tTitle=\"{Title},\"\n\tDescription=\"{Description},\"\n\tInputAction={InputAction},\n\tEnabled={Enabled},\n\tListening={Listening}\n}}";
		}
	}
}
