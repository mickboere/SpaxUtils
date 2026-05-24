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
					if (ctx.started) agent.Actor.SendInput(capturedAct, true);
					else if (ctx.canceled) agent.Actor.SendInput(capturedAct, false);
					return true;
				}, config.bindingInputPriority);
			}
		}

		private void UnsubscribeBindings(ModifierConfig config)
		{
			if (!bindingListeners.TryGetValue(config.modifierAction, out List<object> listeners)) return;

			foreach (object listener in listeners)
			{
				playerInputWrapper.Unsubscribe(listener);
			}
			listeners.Clear();
		}

		// Use a wrapper object as the listener key so modifier and binding subscriptions never collide.
		private object GetModifierKey(ModifierConfig config) => config;
	}
}
