namespace SpaxUtils
{
	/// <summary>
	/// Reason for a game-state transition request.
	/// Keep generic (no project-specific values like "RespawnFromGrave").
	/// </summary>
	public enum GameStateSwitchReason
	{
		Unknown = 0,
		Boot = 1,
		UserRequest = 2,
		LevelChange = 3,
		Respawn = 4
	}
}
