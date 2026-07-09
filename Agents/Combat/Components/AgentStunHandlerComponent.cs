using System;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(100)]
	public class AgentStunHandlerComponent : AgentComponentBase, IStunHandler
	{
		public event Action EnteredStunEvent;
		public event Action ExitedStunEvent;

		public bool Stunned { get; protected set; }

		protected virtual bool DefaultExitBehavior { get; } = true;

		[SerializeField] protected float minStunTime = 0.5f;

		protected CallbackService callbackService;
		protected RigidbodyWrapper rigidbodyWrapper;
		protected FloatOperationModifier controlMod;
		protected HitData stunHit;
		protected TimerClass stunTimer;

		public void InjectDependencies(CallbackService callbackService, RigidbodyWrapper rigidbodyWrapper)
		{
			this.callbackService = callbackService;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		protected virtual void Awake()
		{
			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
		}

		protected virtual void OnEnable()
		{
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
			Agent.ReviveEvent += OnRevive;
		}

		protected virtual void OnDisable()
		{
			rigidbodyWrapper.Control.RemoveModifier(this);
			Agent.ReviveEvent -= OnRevive;
		}

		// A stun active at the moment of death has its timer frozen by the death slow-mo, so on revive it would resume
		// and keep control locked (and the Actor blocked) until it expires. Clear it so revival starts unstunned.
		private void OnRevive()
		{
			if (Stunned)
			{
				ExitStun();
			}
		}

		protected virtual void FixedUpdate()
		{
			if (Stunned && DefaultExitBehavior && stunTimer.Expired)
			{
				ExitStun();
			}
		}

		public virtual void EnterStun(HitData stunHit, float duration = -1f)
		{
			Stunned = true;
			this.stunHit = stunHit;
			controlMod.SetValue(0f);
			stunTimer?.Dispose();
			stunTimer = new TimerClass(duration > 0f ? duration : minStunTime, () => EntityTimeScale, callbackService, UpdateMode.FixedUpdate);
			Agent.Actor.AddBlocker(this);

			EnteredStunEvent?.Invoke();
		}

		public virtual void ExitStun()
		{
			Stunned = false;
			controlMod.SetValue(1f);
			stunTimer?.Dispose();
			stunTimer = null;
			Agent.Actor.RemoveBlocker(this);

			ExitedStunEvent?.Invoke();
		}
	}
}
