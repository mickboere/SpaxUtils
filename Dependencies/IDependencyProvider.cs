using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for components that can provide dependencies before injection.
	/// </summary>
	public interface IDependencyProvider
	{
		/// <summary>
		/// Provides a collection of dependencies to be collected before injection.
		/// </summary>
		public Dictionary<object, object> RetrieveDependencies();
	}
}