using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using CallbackContext = UnityEngine.InputSystem.InputAction.CallbackContext;

namespace SpaxUtils
{
	/// <summary>
	/// Class that wraps around a <see cref="UnityEngine.InputSystem.PlayerInput"/> component.
	/// </summary>
	[RequireComponent(typeof(PlayerInput))]
	public class PlayerInputWrapper : MonoBehaviour
	{
		#region Events

		/// <summary>
		/// Invoked whenever any <see cref="InputAction"/> in the <see cref="UnityEngine.InputSystem.PlayerInput"/> is triggered.
		/// </summary>
		public event Action<CallbackContext> ActionTriggeredEvent;

		/// <summary>
		/// Invoked when the last triggered <see cref="InputAction"/> is from a different device than <see cref="LastDevice"/>.
		/// Beware that this is invoked BEFORE the <see cref="LastDevice"/> is updated.
		/// </summary>
		public event Action<InputDevice> LastInputDeviceChangedEvent;

		/// <summary>
		/// Invoked when the <see cref="PlayerInput"/>'s control scheme has changed.
		/// </summary>
		public event Action<string> ControlSchemeChangedEvent;

		/// <summary>
		/// Invoked when a there is a new input subscription.
		/// (listener, inputAction)
		/// </summary>
		public event Action<object, string> GainedSubscriberEvent;

		/// <summary>
		/// Invoked when an input listener has unsubscribed.
		/// (listener, inputAction)
		/// inputAction is null if the listener has unsubscribed from all inputs.
		/// </summary>
		public event Action<object, string> LostSubscriberEvent;

		/// <summary>
		/// Invoked when the active action maps have changed.
		/// </summary>
		public event Action SwitchedActionMapsEvent;

		#endregion

		#region Properties

		/// <summary>
		/// The <see cref="PlayerInput"/> this wrapper wraps around.
		/// </summary>
		public PlayerInput PlayerInput { get; private set; }

		/// <summary>
		/// The player index of this input wrapper.
		/// </summary>
		public int PlayerIndex => PlayerInput.playerIndex;

		/// <summary>
		/// The <see cref="PlayerInput"/>'s current control scheme.
		/// </summary>
		public string CurrentControlScheme => PlayerInput.currentControlScheme;

		/// <summary>
		/// The last <see cref="InputDevice"/> to trigger an <see cref="InputAction"/>.
		/// </summary>
		public InputDevice LastDevice { get; private set; }

		/// <summary>
		/// The currently enabled <see cref="InputActionMap"/>s.
		/// </summary>
		public string[] ActiveActionMaps => actionMapsCache.Where((m) => m.Value.enabled).Select((n) => n.Key).ToArray();

		#endregion

		private Dictionary<string, Dictionary<object, Action<CallbackContext>>> actionSubscriptions = new Dictionary<string, Dictionary<object, Action<CallbackContext>>>();
		private Dictionary<object, (int prio, string[] maps)> actionMapRequests = new Dictionary<object, (int prio, string[] maps)>();

		private Dictionary<string, InputAction> actionsCache = new Dictionary<string, InputAction>();
		private Dictionary<string, InputActionMap> actionMapsCache = new Dictionary<string, InputActionMap>();

		private bool switchingActionMaps;

		protected void OnEnable()
		{
			PlayerInput = GetComponent<PlayerInput>();
			PlayerInput.neverAutoSwitchControlSchemes = false;

			CollectActionMaps();

			PlayerInput.onActionTriggered += OnActionTriggered;
			PlayerInput.onControlsChanged += OnControlsChanged;
		}

		protected void OnDisable()
		{
			PlayerInput.onActionTriggered -= OnActionTriggered;
			PlayerInput.onControlsChanged -= OnControlsChanged;
		}

		private void OnControlsChanged(PlayerInput playerInput)
		{
			SpaxDebug.Log($"P{PlayerIndex} OnControlsChanged: ", playerInput.currentControlScheme);
			if (playerInput == PlayerInput)
			{
				ControlSchemeChangedEvent?.Invoke(CurrentControlScheme);
			}
		}

		#region InputActions

		/// <summary>
		/// Returns the <see cref="InputAction"/> named <paramref name="inputAction"/>.
		/// </summary>
		/// <param name="inputAction">The name of the <see cref="InputAction"/> you wish to retrieve.</param>
		/// <returns>The <see cref="InputAction"/> named <paramref name="inputAction"/>, if found. Else returns null.</returns>
		public InputAction GetAction(string inputAction)
		{
			if (!actionsCache.ContainsKey(inputAction))
			{
				InputAction action = PlayerInput.actions.FindAction(inputAction, true);
				if (action != null)
				{
					actionsCache[inputAction] = action;
				}
				else
				{
					return null;
				}
			}

			return actionsCache[inputAction];
		}

		/// <summary>
		/// Returns the first <see cref="InputBinding"/> in <see cref="InputAction"/> <paramref name="inputAction"/> that is active within the <paramref name="controlScheme"/>.
		/// </summary>
		/// <param name="inputAction">The name of the <see cref="InputAction"/> to search bindings for.</param>
		/// <param name="controlScheme">Control scheme filter. Leave null to use <see cref="CurrentControlScheme"/>.</param>
		/// <returns>The first <see cref="InputBinding"/> in <see cref="InputAction"/> <paramref name="inputAction"/> that is active within the <paramref name="controlScheme"/>.</returns>
		public InputBinding GetBinding(string inputAction, out string name, string controlScheme = null)
		{
			if (string.IsNullOrWhiteSpace(controlScheme))
			{
				controlScheme = CurrentControlScheme;
			}

			InputBinding binding = GetAction(inputAction).bindings.FirstOrDefault((b) => b.isComposite || (b.groups.Contains(controlScheme) && !b.isPartOfComposite));
			name = binding.isComposite ? binding.name : binding.ToDisplayString();
			return binding;
		}

		public string GetBindingName(string inputAction)
		{
			GetBinding(inputAction, out string name);
			return name;
		}

		/// <summary>
		/// Subscribes the <paramref name="listener"/> to the <see cref="InputAction"/> named <paramref name="inputAction"/>.
		/// <paramref name="callback"/> gets invoked when the action is triggered.
		/// </summary>
		/// <param name="listener">The subscriber object, used for unsubscribing.</param>
		/// <param name="inputAction">The name of the <see cref="InputAction"/> you want to subscribe to.</param>
		/// <param name="callback">The callback that gets invoked when the input action is triggered.</param>
		public void Subscribe(object listener, string inputAction, Action<CallbackContext> callback)
		{
			SpaxDebug.Log($"P{PlayerIndex} Subscribed: ", $"[{inputAction}], {listener}", LogType.Notify, Color.blue, gameObject, 2);

			if (!actionSubscriptions.ContainsKey(inputAction))
			{
				actionSubscriptions.Add(inputAction, new Dictionary<object, Action<CallbackContext>>());
			}

			actionSubscriptions[inputAction][listener] = callback;
			GainedSubscriberEvent?.Invoke(listener, inputAction);
		}

		/// <summary>
		/// Unsubscribes <paramref name="listener"/> from the <see cref="InputAction"/> named <paramref name="inputAction"/>.
		/// If <paramref name="inputAction"/> is null, will unsubscribe the <paramref name="listener"/> from all its subscriptions.
		/// </summary>
		/// <param name="listener">The subscribed object / subscription identifier.</param>
		/// <param name="inputAction">Any specific <see cref="InputAction"/> to unsubscribe from.</param>
		public void Unsubscribe(object listener, string inputAction = null)
		{
			if (inputAction == null)
			{
				// Unsubscribe from all inputActions using this listener.
				foreach (var sub in actionSubscriptions)
				{
					if (sub.Value.ContainsKey(listener))
					{
						sub.Value.Remove(listener);
					}
				}
			}
			else
			{
				// Only unsubscribe the listener from the given inputAction.
				actionSubscriptions[inputAction].Remove(listener);
			}

			LostSubscriberEvent?.Invoke(listener, inputAction);
		}

		/// <summary>
		/// Returns all of the currently subscribed objects.
		/// </summary>
		public List<(object, string)> GetSubscribers()
		{
			return actionSubscriptions.SelectMany((action) => action.Value.Select((sub) => (sub.Key, action.Key))).ToList();
		}

		private void OnActionTriggered(CallbackContext context)
		{
			if (switchingActionMaps)
			{
				// All triggered actions are retriggered when switching maps, this can cause issues when switching maps on input.
				return;
			}

			// Set triggered context's device as current device.
			if (context.control.device != LastDevice)
			{
				LastInputDeviceChangedEvent?.Invoke(context.control.device);
			}
			LastDevice = context.control.device;

			// Generic callback.
			ActionTriggeredEvent?.Invoke(context);

			// InputAction Subscribers.
			if (actionSubscriptions.ContainsKey(context.action.name))
			{
				// Collect callbacks before invoking in case of unsubscription during callback.
				Action<CallbackContext>[] callbacks = actionSubscriptions[context.action.name].Values.ToArray();
				foreach (Action<CallbackContext> callback in callbacks)
				{
					callback.Invoke(context);
				}
			}
		}

		#endregion

		#region InputActionMaps

		/// <summary>
		/// Returns the <see cref="InputActionMap"/> named <paramref name="actionMap"/>.
		/// </summary>
		/// <param name="actionMap">The name of the <see cref="InputActionMap"/> you wish to retrieve.</param>
		/// <returns>The <see cref="InputActionMap"/> named <paramref name="actionMap"/>, if found. Else returns null.</returns>
		public InputActionMap GetActionMap(string actionMap)
		{
			if (!actionMapsCache.ContainsKey(actionMap))
			{
				InputActionMap map = PlayerInput.actions.FindActionMap(actionMap, true);
				if (map != null)
				{
					actionMapsCache[actionMap] = map;
				}
				else
				{
					return null;
				}
			}

			return actionMapsCache[actionMap];
		}

		/// <summary>
		/// Requests for the given <paramref name="actionMaps"/> to be enabled.
		/// If <paramref name="priority"/> is 0, the request will be regarded as non-exclusive.
		/// If <paramref name="priority"/> is higher than 0, the request will be regarded as exclusive,
		/// meaning only the given <paramref name="actionMaps"/> will be enabled and everything else disabled - if the priority is the highest.
		/// Disables all cached <see cref="InputActionMap"/>s except for those present in <paramref name="actionMaps"/>.
		/// </summary>
		/// <param name="context">Object or context identifier for whatever is requesting these actionmaps.</param>
		/// <param name="priority">The priority of this request. 0 means non-exclusive, > 0 means highest priority gets rights.</param>
		/// <param name="actionMaps">The names of the <see cref="InputActionMap"/>s to enable.</param>
		public void RequestActionMaps(object context, int priority, params string[] actionMaps)
		{
			if (actionMapRequests.ContainsKey(context))
			{
				actionMapRequests.Remove(context);
			}

			if (actionMaps == null || actionMaps.Length < 1)
			{
				return;
			}

			actionMapRequests.Add(context, (priority, actionMaps));
			SwitchToHighestPriorityActionMap();
		}

		public void CompleteActionMapRequest(object context)
		{
			if (context == null || !actionMapRequests.ContainsKey(context))
			{
				return;
			}

			actionMapRequests.Remove(context);
			SwitchToHighestPriorityActionMap();
		}

		private void SwitchToHighestPriorityActionMap()
		{
			int highestPrio = actionMapRequests.Count > 0 ? actionMapRequests.Max((h) => h.Value.prio) : 0;

			if (highestPrio == 0)
			{
				HashSet<string> actionMaps = new HashSet<string>();
				foreach (KeyValuePair<object, (int prio, string[] maps)> request in actionMapRequests)
				{
					foreach (string map in request.Value.maps)
					{
						actionMaps.Add(map);
					}
				}
				SwitchActionMaps(actionMaps.ToArray());
			}
			else
			{
				SwitchActionMaps(actionMapRequests.LastOrDefault((r) => r.Value.prio == highestPrio).Value.maps);
			}
		}

		private void SwitchActionMaps(params string[] actionMaps)
		{
			switchingActionMaps = true;
			foreach (KeyValuePair<string, InputActionMap> map in actionMapsCache)
			{
				if (actionMaps.Contains(map.Key))
				{
					map.Value.Enable();
				}
				else
				{
					map.Value.Disable();
				}
			}
			switchingActionMaps = false;
			SwitchedActionMapsEvent?.Invoke();
			if (PlayerInput != null)
			{
				//SpaxDebug.Log($"P{PlayerIndex} InputActionMaps changed. ", $"Enabled maps: ({string.Join(", ", ActiveActionMaps)}).");
			}
		}

		private void CollectActionMaps()
		{
			foreach (InputActionMap map in PlayerInput.actions.actionMaps)
			{
				actionMapsCache[map.name] = map;
			}
		}

		#endregion

	}
}
