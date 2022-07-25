namespace SpaxUtils
{
	public interface IFactory<T> : IFactory
	{
	}

	/// <summary>
	/// Interface for factories used to create instances of whatever generic type is implemented.
	/// </summary>
	public interface IFactory
	{
		public object Create(IDependencyManager dependencies);
	}
}
