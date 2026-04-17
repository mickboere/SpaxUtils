namespace SpaxUtils
{
	/// <summary>
	/// Generic communications message containing a string.
	/// </summary>
	public class CommsMsg
	{
		public string Message { get; }

		public CommsMsg(string message)
		{
			Message = message;
		}
	}
}
