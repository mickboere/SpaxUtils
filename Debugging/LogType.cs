namespace SpaxUtils
{
	/// <summary>
	/// The different types of logs.
	/// </summary>
	public enum LogType
	{
		Log,    // Debug.Log
		Warning,// Debug.LogWarning
		Error,  // Debug.LogError
		Notify, // Debug.Log, but only logged when "Debugging" is set to true
	}
}
