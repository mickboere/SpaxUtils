using System;
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
		public event Action<Option> MadeAvailableEvent;
		public event Action<Option> MadeUnavailableEvent;

		public string Title { get; }
		public string Description { get; }
		public string InputAction { get; }
		public bool HasInputAction => !string.IsNullOrEmpty(InputAction);
		public bool Available { get; private set; }

		private readonly bool pickOnInput;
		private readonly Action<Option> onPickedCallback;
		private readonly Action<CallbackContext> onInputCallback;

		private PlayerInputWrapper playerInputWrapper;

		public Option(
			string title,
			string description,
			Action<Option> onPickedCallback,
			string inputAction = null,
			bool pickOnInput = true,
			Action<CallbackContext> onInputCallback = null,
			PlayerInputWrapper playerInputWrapper = null)
		{
			Title = title;
			Description = description;
			this.onPickedCallback = onPickedCallback;
			InputAction = inputAction;
			this.pickOnInput = pickOnInput;
			this.onInputCallback = onInputCallback;
			this.playerInputWrapper = playerInputWrapper;
		}

		/// <summary>
		/// Create a new input-callback-only option.
		/// </summary>
		public Option(string title, string inputAction, Action<CallbackContext> onInputCallback, PlayerInputWrapper playerInputWrapper)
		{
			Title = title;
			InputAction = inputAction;
			this.onInputCallback = onInputCallback;
			this.playerInputWrapper = playerInputWrapper;

			Description = "";
			pickOnInput = false;
			onPickedCallback = null;
		}

		public void Dispose()
		{
			MakeUnavailable();
		}

		/// <summary>
		/// Makes this option available and starts listening for input.
		/// </summary>
		public void MakeAvailable(PlayerInputWrapper playerInputWrapper = null)
		{
			if (playerInputWrapper != null)
			{
				this.playerInputWrapper = playerInputWrapper;
			}

			if (Available)
			{
				SpaxDebug.Error("Option is already available.", ToString());
				return;
			}

			if (HasInputAction && this.playerInputWrapper == null)
			{
				SpaxDebug.Error("Option has an input action but no PlayerInputWrapper.", ToString());
				return;
			}

			if (!HasInputAction)
			{
				return;
			}

			this.playerInputWrapper.Subscribe(this, InputAction,
				(inputContext) =>
				{
					onInputCallback?.Invoke(inputContext);
					ReceivedInputEvent?.Invoke(inputContext);

					if (pickOnInput && inputContext.canceled)
					{
						Pick();
					}
				});

			Available = true;
			MadeAvailableEvent?.Invoke(this);
		}

		/// <summary>
		/// Makes this option unavailable and stops listening for input.
		/// </summary>
		public void MakeUnavailable()
		{
			if (!Available)
			{
				return;
			}

			if (playerInputWrapper != null)
			{
				playerInputWrapper.Unsubscribe(this);
			}
			Available = false;
			MadeUnavailableEvent?.Invoke(this);
		}

		/// <summary>
		/// Pick this option and invoke its callback.
		/// </summary>
		public void Pick()
		{
			PickedEvent?.Invoke(this);
			onPickedCallback?.Invoke(this);
		}

		public override string ToString()
		{
			return $"Option\n{{\n\tTitle=\"{Title}\"\n\tDescription=\"{Description}\"\n\tInputAction={InputAction}\n}}\n";
		}
	}
}
