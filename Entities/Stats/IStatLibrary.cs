namespace SpaxUtils
{
	/// <summary>
	/// Interface for a library containing references to <see cref="IStatConfiguration"/>s through <see cref="string"/> identifiers.
	/// </summary>
	public interface IStatLibrary
	{
		bool TryGet(string identifier, out IStatConfiguration configuration);

		IStatConfiguration Get(string identifier);
	}
}
