using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Rule that becomes valid once a specified message is received through the global comms.
	/// </summary>
	[NodeWidth(300)]
	public class GlobalCommsRuleNode : RuleNodeBase
	{
		public override bool Valid => _valid;
		private bool _valid;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule inConnection;
		[SerializeField, ConstDropdown(typeof(ICommsMsgs))] private string message;

		private GlobalComms globalComms;

		public void InjectDependencies(GlobalComms globalComms)
		{
			this.globalComms = globalComms;
			_valid = false;
		}

		public override void OnEnteringState()
		{
			base.OnEnteringState();
			globalComms.Listen<CommsMsg>(this, OnMsg);
		}

		public override void OnExitingState()
		{
			base.OnExitingState();
			globalComms.StopListening(this);
		}

		private void OnMsg(CommsMsg msg)
		{
			if (msg.Message == message)
			{
				_valid = true;
			}
		}
	}
}
