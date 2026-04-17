using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="StateMachineBehaviour"/> that signals it's defined state to the <see cref="AnimatorStateObserver"/>.
	/// </summary>
	public class NotifyAnimatorStateChange : StateMachineBehaviour
	{
		[SerializeField, ConstDropdown(typeof(IAnimatorStates))] private string animatorState;

		private AnimatorStateObserver _animatorStateObserver;

		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			GetAnimatorStateObserver(animator)?.SignalHeartbeat(animatorState);
		}

		public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			GetAnimatorStateObserver(animator)?.SignalHeartbeat(animatorState);
		}

		private AnimatorStateObserver GetAnimatorStateObserver(Animator animator)
		{
			if (_animatorStateObserver == null)
			{
				_animatorStateObserver = animator.GetComponentInParent<AnimatorStateObserver>();
			}

			return _animatorStateObserver;
		}
	}
}
