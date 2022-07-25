namespace SpaxUtils
{
	/// <summary>
	/// Interface for entity components, contains a reference to the entity it belongs to.
	/// </summary>
	public interface IEntityComponent : IDependency
	{
		IEntity Entity { get; }
	}
}
