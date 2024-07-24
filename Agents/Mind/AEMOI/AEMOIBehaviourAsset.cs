using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class implementing <see cref="IMindBehaviour"/> for <see cref="IMind"/> behaviour assets.
	/// </summary>
	public abstract class AEMOIBehaviourAsset : BehaviourAsset, IMindBehaviour
	{
		public virtual int Priority => priority;
		public virtual bool Interuptable { get; protected set; } = true;

		protected IAgent Agent { get; private set; }
		protected IMind Mind => Agent.Mind;
		protected CallbackService CallbackService { get; private set; }

		[SerializeField] protected int priority;
		[SerializeField] protected Vector8 motivation;
		[SerializeField, HideInInspector] protected bool requireState;
		[SerializeField, Conditional(nameof(requireState), drawToggle: true), ConstDropdown(typeof(IStateIdentifierConstants))] protected string brainState;

		public void InjectDependencies(IAgent agent, CallbackService callbackService)
		{
			Agent = agent;
			CallbackService = callbackService;
		}

		public virtual bool Valid(Vector8 motivation, IEntity target, out float distance)
		{
			if (motivation > this.motivation)
			{
				distance = motivation.Distance(this.motivation);
				return true;
			}

			distance = float.MaxValue;
			return false;
		}

		public override void Start()
		{
			base.Start();
			if (requireState)
			{
				Agent.Brain.TryTransition(brainState);
			}
		}

		public override void Stop()
		{
			base.Stop();
		}
	}
}
