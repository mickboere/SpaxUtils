using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Simple debug log node.
	/// </summary>
	[NodeWidth(300)]
	public class DebugNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		[SerializeField] private string header;
		[SerializeField] private string body;
		[SerializeField] private LogType logType = LogType.Log;
		[SerializeField] private Color logColor = Color.white;

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			SpaxDebug.Log(header, " " + body, logType, logColor);
		}
	}
}