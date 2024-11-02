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
		public bool Active { get; private set; }

		private readonly bool pickOnInput;
		private readonly bool eatInput;
		private readonly int prio;
		private readonly Action<Option> onPickedCallback;
		private readonly Action<CallbackContext> onInputCallback;

		private readonly IContextManager contextManager;
		private readonly string context;

		private PlayerInputWrapper playerInputWrapper;

		private bool enabled;
		private bool contextActive;

		public Option(
			string title,
			string description,
			Action<Option> onPickedCallback,
			string inputAction = null,
			bool pickOnInput = true,
			bool eatInput = false,
			int prio = 0,
			Action<CallbackContext> onInputCallback = null,
			PlayerInputWrapper playerInputWrapper = null,
			IContextManager contextManager = null,
			string context = "")
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
			this.contextManager = contextManager;
			this.context = context;

			VerifyContext();
			if (contextManager != null)
			{
				contextManager.ContextChangedEvent += OnContextChangedEvent;
			}
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
			IContextManager contextManager = null,
			string context = "")
		{
			Title = title;
			InputAction = inputAction;
			this.onInputCallback = onInputCallback;
			this.playerInputWrapper = playerInputWrapper;
			this.eatInput = eatInput;
			this.prio = prio;
			this.contextManager = contextManager;
			this.context = context;

			Description = "";
			onPickedCallback = null;
			pickOnInput = false;

			VerifyContext();
			if(contextManager != null)
			{
				contextManager.ContextChangedEvent += OnContextChangedEvent;
			}
		}

		public void Dispose()
		{
			Deactivate();
			if (contextManager != null)
			{
				contextManager.ContextChangedEvent -= OnContextChangedEvent;
			}
		}

		/// <summary>
		/// Enables this option to allow it to start listening for input while the context is active.
		/// </summary>
		public void Enable(PlayerInputWrapper playerInputWrapper = null)
		{
			if (enabled)
			{
				return;
			}

			enabled = true;

			if (playerInputWrapper != null)
			{
				this.playerInputWrapper = playerInputWrapper;
			}

			if (!Active && contextActive)
			{
				Activate();
			}
		}

		/// <summary>
		/// Disables this option to make it stop listening for input.
		/// </summary>
		public void Disable()
		{
			if (!enabled)
			{
				return;
			}

			enabled = false;

			if (Active)
			{
				Deactivate();
			}
		}

		/// <summary>
		/// Pick this option and invoke its callback.
		/// </summary>
		public void Pick()
		{
			PickedEvent?.Invoke(this);
			onPickedCallback?.Invoke(this);
		}

		private void Activate()
		{
			if (Active || !HasInputAction)
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
					onInputCallback?.Invoke(inputContext);
					ReceivedInputEvent?.Invoke(inputContext);

					if (pickOnInput && inputContext.canceled)
					{
						Pick();
					}

					return eatInput;
				},
				prio);

			Active = true;
			MadeAvailableEvent?.Invoke(this);
		}

		private void Deactivate()
		{
			if (!Active)
			{
				return;
			}

			playerInputWrapper.Unsubscribe(this);
			Active = false;
			MadeUnavailableEvent?.Invoke(this);
		}

		private void VerifyContext()
		{
			if (contextManager == null || string.IsNullOrEmpty(context))
			{
				contextActive = true;
			}
			else
			{
				contextActive = contextManager.IsActive(context);
				if (!Active && enabled && contextActive)
				{
					Activate();
				}
				else if (Active && !contextActive)
				{
					Deactivate();
				}
			}
		}

		private void OnContextChangedEvent()
		{
			VerifyContext();
		}

		public override string ToString()
		{
			return $"Option\n{{\n\tTitle=\"{Title}\"\n\tDescription=\"{Description}\"\n\tInputAction={InputAction}\n}}\n";
		}
	}
}
