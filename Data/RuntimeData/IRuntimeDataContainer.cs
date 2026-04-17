namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects containing a <see cref="RuntimeDataCollection"/>.
	/// </summary>
	public interface IRuntimeDataContainer
	{
		RuntimeDataCollection RuntimeData { get; }
	}
}
