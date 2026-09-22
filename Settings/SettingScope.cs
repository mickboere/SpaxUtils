namespace SpaxUtils
{
	/// <summary>
	/// Where a <see cref="Setting"/>'s override is stored.
	/// </summary>
	public enum SettingScope
	{
		Global, // Cross-profile, stored in GLOBAL.
		Profile, // Per save, stored in the current profile.
		Player // Cross-profile per player index, stored in GLOBAL.
	}
}
