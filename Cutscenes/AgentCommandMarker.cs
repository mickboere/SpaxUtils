using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SpaxUtils
{
	/// <summary>
	/// Timeline marker that sends an <see cref="AgentCommandMsg"/> to an agent's communication channel
	/// when the playhead crosses it. Dispatched by any <see cref="INotificationReceiver"/> registered
	/// on the <see cref="PlayableDirector"/>'s outputs (e.g. <see cref="CutsceneDirector"/>).
	/// </summary>
	public class AgentCommandMarker : Marker, INotification
	{
		public PropertyName id => new PropertyName(command.ToString());

		public string AgentId => agentId;
		public AgentCommand Command => command;
		public string Parameter => parameter;
		public bool Immediate => immediate;

		[SerializeField, ConstDropdown(typeof(IIdentificationIdentifiers), inputField: true)] private string agentId;
		[SerializeField] private AgentCommand command;
		[SerializeField] private string parameter;
		[SerializeField] private bool immediate;
	}
}
