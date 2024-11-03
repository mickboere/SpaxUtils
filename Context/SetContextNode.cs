using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	public class SetContextNode : StateComponentNodeBase
	{
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers))] private string context;
		[SerializeField] private bool push;
		[SerializeField] private bool pop;

		private IContextManager contextManager;

		public void InjectDependencies(IContextManager contextManager)
		{
			this.contextManager = contextManager;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			if (push)
			{
				contextManager.Push(context);
			}
			else
			{
				contextManager.Switch(context);
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			if (pop)
			{
				contextManager.Pop(context);
			}
		}
	}
}
