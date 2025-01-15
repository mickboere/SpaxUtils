namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can create and append dependencies to the dependency manager before injection.
	/// </summary>
	public interface IDependencyFactory
	{
		void Bind(IDependencyManager dependencyManager);
	}
}
