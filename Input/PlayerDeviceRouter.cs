using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

namespace SpaxUtils
{
	/// <summary>
	/// While running, decides where unused devices go: Start on a free gamepad requests a join, the rest may switch player one.
	/// Replaces Unity's auto-switch, which only works in single player and would hand a joining pad to player one.
	/// </summary>
	public class PlayerDeviceRouter : IService, IDisposable
	{
		/// <summary>
		/// Invoked when Start is pressed on a gamepad nobody uses, while joining is allowed.
		/// </summary>
		public event Action<Gamepad> JoinRequestedEvent;

		/// <summary>
		/// Invoked with the index of a joined player whose device was unplugged.
		/// </summary>
		public event Action<int> DeviceLostEvent;

		public bool Routing => owners.Count > 0;

		private readonly PlayerInputService playerInputService;
		private readonly PlayerInputSettings inputSettings;
		private readonly List<object> owners = new List<object>();
		private Func<bool> canJoin;
		private bool listening; // Whether we hold one count of the shared unpaired-activity counter.

		// Player one's own pad, the only one it may switch to while connected. Input can also surface on a mirror
		// device nobody owns, so without a home pad player one only adopts a new one while alone.
		private Gamepad homePad;

		private bool HasHomePad => homePad != null && homePad.added;
		private bool PlayerOneKeepsPad => inputSettings.PlayerOneInput.Value == PlayerOneDevices.KeyboardMouseAndGamepad;

		// Device/pairing trace for Debuddy, tag [DEVROUTE]. Hooked outside routing so auto-switch shows up too.
		private static readonly bool logDevices = false;
		private const string LOG_TAG = "[DEVROUTE]";

		public PlayerDeviceRouter(PlayerInputService playerInputService, PlayerInputSettings inputSettings)
		{
			this.playerInputService = playerInputService;
			this.inputSettings = inputSettings;
			inputSettings.PlayerOneInput.ChangedEvent += OnPlayerOneInputChanged;

			if (logDevices)
			{
				InputSystem.onDeviceChange += LogDeviceChange;
				InputUser.onChange += LogUserChange;
			}
		}

		public void Dispose()
		{
			owners.Clear();
			Unhook();
			inputSettings.PlayerOneInput.ChangedEvent -= OnPlayerOneInputChanged;
			InputSystem.onDeviceChange -= LogDeviceChange;
			InputUser.onChange -= LogUserChange;
		}

		/// <summary>
		/// Starts routing for <paramref name="owner"/>; joins are only requested while <paramref name="canJoin"/> allows.
		/// </summary>
		public void Start(object owner, Func<bool> canJoin)
		{
			this.canJoin = canJoin;
			owners.Add(owner);
			if (owners.Count > 1)
			{
				return;
			}

			if (playerInputService.TryGet(0, out PlayerInputWrapper main))
			{
				main.SetAutoSwitch(false);
				homePad = main.PlayerInput.devices.OfType<Gamepad>().FirstOrDefault() ?? homePad;
				if (!PlayerOneKeepsPad)
				{
					ReleasePad(main);
				}
			}

			InputUser.onUnpairedDeviceUsed -= OnUnpairedDeviceUsed;
			InputUser.onUnpairedDeviceUsed += OnUnpairedDeviceUsed;
			InputUser.onChange -= OnInputUserChange;
			InputUser.onChange += OnInputUserChange;
			if (!listening)
			{
				InputUser.listenForUnpairedDeviceActivity++;
				listening = true;
			}

			Log("Routing started");
		}

		/// <summary>
		/// Stops routing for <paramref name="owner"/>; routing ends once no owner remains.
		/// </summary>
		public void Stop(object owner)
		{
			if (!owners.Remove(owner) || owners.Count > 0)
			{
				return;
			}

			Unhook();
			if (playerInputService.TryGet(0, out PlayerInputWrapper main))
			{
				main.SetAutoSwitch(true);
			}

			Log("Routing stopped, Unity auto-switch back on");
		}

		private void Unhook()
		{
			InputUser.onUnpairedDeviceUsed -= OnUnpairedDeviceUsed;
			InputUser.onChange -= OnInputUserChange;
			// Only release our own count: PlayerInput shares the counter and throws if it goes negative.
			if (listening && InputUser.listenForUnpairedDeviceActivity > 0)
			{
				InputUser.listenForUnpairedDeviceActivity--;
			}
			listening = false;
		}

		private void OnUnpairedDeviceUsed(InputControl control, InputEventPtr eventPtr)
		{
			// While player one may keep a pad but has none, the first pad used is its own, even via Start.
			// The pad stays unpaired until the join is processed, so the press can't trigger anyone's actions.
			if (control.device is Gamepad gamepad && control == gamepad.startButton &&
				(!PlayerOneKeepsPad || (HasHomePad && gamepad != homePad)) && canJoin != null && canJoin())
			{
				Log($"Join requested by {Describe(gamepad)}");
				JoinRequestedEvent?.Invoke(gamepad);
				return;
			}

			if (playerInputService.TryGet(0, out PlayerInputWrapper main) && CanSwitchMain(main, control.device) &&
				main.TrySwitchToDevice(control.device))
			{
				Log($"Player 1 switched to {Describe(control.device)} via {control.path}");
				if (control.device is Gamepad pad)
				{
					homePad = pad;
				}
			}
			else if (control.device is Gamepad refused && control == refused.startButton)
			{
				// Start only: every other unpaired input would flood the log with stick noise.
				Log($"Start on unpaired {Describe(refused)} did nothing (canJoin={canJoin != null && canJoin()})");
			}
		}

		// Player one only switches between keyboard & mouse and one gamepad; joysticks have no bindings.
		private bool CanSwitchMain(PlayerInputWrapper main, InputDevice device)
		{
			if (device is Keyboard || device is Mouse)
			{
				return true;
			}
			if (!PlayerOneKeepsPad || !(device is Gamepad) || main.PlayerInput.devices.Any((paired) => paired is Gamepad))
			{
				return false;
			}

			// A connected home pad is final, else a joiner's stick nudge before Start would hand their pad to player one.
			if (HasHomePad)
			{
				return device == homePad;
			}
			return playerInputService.Wrappers.Values.Count((wrapper) => wrapper != null) <= 1;
		}

		// Moves player one onto keyboard & mouse, freeing its gamepad to join with Start.
		private void ReleasePad(PlayerInputWrapper main)
		{
			Gamepad[] pads = main.PlayerInput.devices.OfType<Gamepad>().ToArray();
			if (pads.Length == 0 || (Keyboard.current != null && main.TrySwitchToDevice(Keyboard.current)))
			{
				return;
			}

			foreach (Gamepad pad in pads)
			{
				main.PlayerInput.user.UnpairDevice(pad);
			}
		}

		private void OnPlayerOneInputChanged(Setting setting, int player)
		{
			// Only levels are routed; menus keep Unity's auto-switch, so a pad-only player can always get back here.
			if (Routing && !PlayerOneKeepsPad && playerInputService.TryGet(0, out PlayerInputWrapper main))
			{
				ReleasePad(main);
				Log("Player 1 set to keyboard & mouse only, gamepad released");
			}
		}

		private void OnInputUserChange(InputUser user, InputUserChange change, InputDevice device)
		{
			if (change != InputUserChange.DeviceLost)
			{
				return;
			}

			foreach (KeyValuePair<int, PlayerInputWrapper> wrapper in playerInputService.Wrappers)
			{
				if (wrapper.Key > 0 && wrapper.Value != null && wrapper.Value.PlayerInput != null &&
					wrapper.Value.PlayerInput.user == user)
				{
					DeviceLostEvent?.Invoke(wrapper.Key);
				}
			}
		}

		#region Logging

		private void LogDeviceChange(InputDevice device, InputDeviceChange change)
		{
			Log($"Device {change}: {Describe(device)}");
		}

		// Every pairing change lands here whoever caused it, Unity's own auto-switch included.
		private void LogUserChange(InputUser user, InputUserChange change, InputDevice device)
		{
			Log($"{PlayerOf(user)} {change}" + (device == null ? string.Empty : $": {Describe(device)}"));
		}

		private void Log(string message)
		{
			if (logDevices)
			{
				SpaxDebug.Log(LOG_TAG, $"{message}\n{Snapshot()}");
			}
		}

		private string Snapshot()
		{
			StringBuilder snapshot = new StringBuilder($"  routing={Routing} home={Describe(homePad)}");

			foreach (KeyValuePair<int, PlayerInputWrapper> wrapper in playerInputService.Wrappers)
			{
				PlayerInput input = wrapper.Value == null ? null : wrapper.Value.PlayerInput;
				if (input == null)
				{
					continue;
				}

				snapshot.Append($"\n  Player {wrapper.Key + 1}: autoSwitch={!input.neverAutoSwitchControlSchemes}" +
					$" scheme={input.currentControlScheme}" +
					$" devices=[{string.Join(", ", input.devices.Select(Describe))}]");
			}

			return snapshot.ToString();
		}

		private string PlayerOf(InputUser user)
		{
			foreach (KeyValuePair<int, PlayerInputWrapper> wrapper in playerInputService.Wrappers)
			{
				if (wrapper.Value != null && wrapper.Value.PlayerInput != null && wrapper.Value.PlayerInput.user == user)
				{
					return $"Player {wrapper.Key + 1}";
				}
			}

			return $"User #{user.index}";
		}

		private static string Describe(InputDevice device)
		{
			return device == null ? "none" : $"{device.displayName} #{device.deviceId} <{device.layout}>";
		}

		#endregion Logging
	}
}
