namespace SpaxUtils
{
	/// <summary>
	/// Interface that, when implemented, must provide a key of type <see cref="object"/>, that can be used when binding it to a database.
	/// </summary>
	public interface IBindingKeyProvider
	{
		object BindingKey { get; }
	}
}
