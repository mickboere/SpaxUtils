namespace SpaxUtils
{
	/// <summary>
	/// Optional lifecycle hook for dependencies that must run logic after being constructed and bound.
	/// Use this to avoid circular dependencies caused by side effects in constructors.
	/// </summary>
	public interface IInitializable
	{
		void Initialize();
	}
}
