using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface to mark a <see cref="Component"/> as a Service, meaning the <see cref="IDependencyManager"/> is allowed to attempt to create an instance of it.
	/// </summary>
	internal interface IComponentService : IService
	{
	}
}