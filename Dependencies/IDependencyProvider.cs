using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can provide pre-existing dependencies before injection.
	/// </summary>
	public interface IDependencyProvider
	{
		/// <summary>
		/// Provides a collection of dependencies to be collected before injection.
		/// </summary>
		public Dictionary<object, object> RetrieveDependencies();
	}
}