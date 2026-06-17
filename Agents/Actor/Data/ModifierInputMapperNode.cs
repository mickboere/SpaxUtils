using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using UnityEngine;
using CallbackContext = UnityEngine.InputSystem.InputAction.CallbackContext;

namespace SpaxUtils
{
	/// <summary>
	/// While active, tracks modifier buttons (e.g. R1/L1) and subscribes to face-button
	/// input actions at elevated priority whenever a modifier is held. Each modifier+button
	/// pair dispatches a dedicated act (Right1…Right4 / Left1…Left4). If no performer
	/// supports the act, the input is not eaten and falls through to normal subscribers
	/// (jump, dash, etc. still fire).
	/// </summary>
	[NodeWidth(250)]
	public class ModifierInputMapperNode : StateComponentNodeBase
	{
		[Serializable]
		public class ModifierBinding
		{
			[ConstDropdown(typeof(IInputActions))] public string inputAction;
			[ConstDropdown(typeof(IActIdentifiers))] public string act;
		}

		[Serializable]
		public class ModifierConfig
		{
			[ConstDropdown(typeof(IInputActions))] public string modifierAction;
			public int bindingInputPriority = 20;
			public List<ModifierBinding> bindings;
		}

		[SerializeField] private List<ModifierConfig> modifiers;

		private PlayerInputWrapper playerInputWrapper;
		private IAgent agent;

		// Per modifier action: list of unique listener objects created for each binding subscription.
		private Dictionary<string, List<object>> bindingListeners = new();

		// Buttons currently held down (per binding listener), and listeners kept alive past a modifier-release
		// because their button was still held — they self-unsubscribe once their own button comes up, so releasing
		// the modifier (e.g. Parry) never strands a mid-charge bound act (e.g. a charging kick).
		private readonly HashSet<object> heldBindings = new();
		private readonly HashSet<object> pendingUnsubscribe = new();

		public void InjectDependencies(PlayerInputWrapper playerInputWrapper, IAgent agent)
		{
			this.playerInputWrapper = playerInputWrapper;
			this.agent = agent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			foreach (ModifierConfig config in modifiers)
			{
				bindingListeners[config.modifierAction] = new List<object>();
				ModifierConfig captured = config;

				playerInputWrapper.Subscribe(GetModifierKey(config), config.modifierAction, ctx =>
				{
					if (ctx.started) SubscribeBindings(captured);
					else if (ctx.canceled) UnsubscribeBindings(captured);
					return false;
				}, config.bindingInputPriority);
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			foreach (ModifierConfig config in modifiers)
			{
				playerInputWrapper.Unsubscribe(GetModifierKey(config));
				UnsubscribeBindings(config);
			}

			// Force-clean any bindings deferred while their button was held — the state is exiting, so they go now.
			foreach (object listener in pendingUnsubscribe)
			{
				playerInputWrapper.Unsubscribe(listener);
			}
			pendingUnsubscribe.Clear();
			heldBindings.Clear();
			bindingListeners.Clear();
		}

		private void SubscribeBindings(ModifierConfig config)
		{
			if (config.bindings == null) return;

			foreach (ModifierBinding binding in config.bindings)
			{
				object listener = new object();
				bindingListeners[config.modifierAction].Add(listener);

				string capturedAct = binding.act;
				string capturedInput = binding.inputAction;

				playerInputWrapper.Subscribe(listener, capturedInput, ctx =>
				{
					if (!agent.Actor.SupportsAct(capturedAct)) return false;
					if (ctx.started)
					{
						agent.Actor.SendInput(capturedAct, true);
						heldBindings.Add(listener);
					}
					else if (ctx.canceled)
					{
						agent.Actor.SendInput(capturedAct, false);
						heldBindings.Remove(listener);
						// If the modifier was released while this button was still held, the binding was kept alive
						// solely to deliver this release — now finish tearing it down.
						if (pendingUnsubscribe.Remove(listener))
						{
							playerInputWrapper.Unsubscribe(listener);
						}
					}
					return true;
				}, config.bindingInputPriority);
			}
		}

		private void UnsubscribeBindings(ModifierConfig config)
		{
			if (!bindingListeners.TryGetValue(config.modifierAction, out List<object> listeners)) return;

			foreach (object listener in listeners)
			{
				if (heldBindings.Contains(listener))
				{
					// Button still held (e.g. charging a kick) — keep the binding alive so its release still lands.
					// The modifier only gates ACCESS to new bound acts; it must not cancel one already in progress.
					pendingUnsubscribe.Add(listener);
				}
				else
				{
					playerInputWrapper.Unsubscribe(listener);
				}
			}
			listeners.Clear();
		}

		// Use a wrapper object as the listener key so modifier and binding subscriptions never collide.
		private object GetModifierKey(ModifierConfig config) => config;
	}
}
