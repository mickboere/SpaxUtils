namespace SpaxUtils
{
	/// <summary>
	/// Message sent through an agent's <see cref="ICommunicationChannel"/> to issue a command.
	/// </summary>
	public class AgentCommandMsg
	{
		/// <summary>
		/// The command to execute.
		/// </summary>
		public AgentCommand Command { get; }

		/// <summary>
		/// Command-specific parameter (e.g. POI entity ID for <see cref="AgentCommand.OccupyPOI"/>).
		/// </summary>
		public string Parameter { get; }

		/// <summary>
		/// When true, the command should be executed immediately (e.g. teleport to POI instead of navigating).
		/// </summary>
		public bool Immediate { get; }

		public AgentCommandMsg(AgentCommand command, string parameter = null, bool immediate = false)
		{
			Command = command;
			Parameter = parameter;
			Immediate = immediate;
		}
	}
}
