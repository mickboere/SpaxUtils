namespace SpaxUtils
{
	/// <summary>
	/// Persists <see cref="Setting"/> overrides. Null data means "no override".
	/// </summary>
	public interface ISettingsStore
	{
		object Read(Setting setting, int player);

		void Write(Setting setting, int player, object data);
	}
}
