namespace SpaxUtils
{
	/// <summary>
	/// Interface for entity components, contains a reference to the entity it belongs to.
	/// </summary>
	public interface IEntityComponent : IDependency
	{
		/// <summary>
		/// The <see cref="IEntity"/> this component belongs to.
		/// </summary>
		IEntity Entity { get; }
	}
}
