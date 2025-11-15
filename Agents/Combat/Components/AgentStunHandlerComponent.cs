using System;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(100)]
	public class AgentStunHandlerComponent : AgentComponentBase, IStunHandler
	{
		#region Tooltips
		private const string TT_RECOVERY_THRESH = "Upper velocity threshold below which Agent begins to recover.";
		private const string TT_RECOVERED_THRESH = "Lower velocity threshold below which control is fully returned to Agent.";
		#endregion Tooltips

		public event Action EnteredStunEvent;
		public event Action ExitedStunEvent;

		public bool Stunned { get; protected set; }

		protected virtual bool DefaultExitBehavior { get; } = true;

		[SerializeField] protected float minStunTime = 0.5f;
		[SerializeField, Tooltip(TT_RECOVERY_THRESH)] protected float recoveryThreshold = 1.5f;
		[SerializeField, Tooltip(TT_RECOVERED_THRESH)] protected float recoveredThreshold = 0.5f;

		protected CallbackService callbackService;
		protected RigidbodyWrapper rigidbodyWrapper;
		protected FloatFuncModifier controlMod;
		protected HitData stunHit;
		protected TimerClass stunTimer;
		protected float stunAmount;

		public void InjectDependencies(CallbackService callbackService, RigidbodyWrapper rigidbodyWrapper)
		{
			this.callbackService = callbackService;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		protected virtual void Awake()
		{
			controlMod = new FloatFuncModifier(ModMethod.Absolute, (float f) => f * 1f - stunAmount);
		}

		protected virtual void OnEnable()
		{
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
		}

		protected virtual void OnDisable()
		{
			rigidbodyWrapper.Control.RemoveModifier(this);
		}

		protected virtual void FixedUpdate()
		{
			if (Stunned)
			{
				UpdateStun();
			}
		}

		public virtual void EnterStun(HitData stunHit, float duration = -1f)
		{
			Stunned = true;
			stunAmount = 1f;
			this.stunHit = stunHit;
			stunTimer = new TimerClass(duration > 0f ? duration : minStunTime, () => EntityTimeScale, callbackService, UpdateMode.FixedUpdate);
			Agent.Actor.AddBlocker(this);

			EnteredStunEvent?.Invoke();
		}

		public virtual void ExitStun()
		{
			Stunned = false;
			Agent.Actor.RemoveBlocker(this);
			stunAmount = 0f;

			ExitedStunEvent?.Invoke();
		}

		protected virtual void UpdateStun()
		{
			stunAmount = Mathf.Max(stunTimer.Progress.InvertClamped(), Mathf.InverseLerp(recoveredThreshold, recoveryThreshold, rigidbodyWrapper.Speed));
			if (DefaultExitBehavior && stunTimer.Expired && rigidbodyWrapper.Speed < recoveredThreshold)
			{
				ExitStun();
			}
		}
	}
}
