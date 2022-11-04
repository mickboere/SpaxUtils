namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects containing a unique ID of type <see cref="string"/>.
	/// </summary>
	public interface IUnique
	{
		/// <summary>
		/// Unique string used to identify this object only.
		/// </summary>
		string ID { get; }
	}
}
