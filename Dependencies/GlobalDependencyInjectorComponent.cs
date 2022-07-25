using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// You are highly discouraged from using this component.
	/// Instead you should make sure your spawned object has its dependencies injected externally.
	/// 
	/// This class will get all of the <see cref="MonoBehaviour"/>s on the current object and tries to inject their dependencies.
	/// </summary>
	[DefaultExecutionOrder(-10)]
	public class GlobalDependencyInjectorComponent : MonoBehaviour
	{
		[SerializeField] private bool includeChildren = true;
		[SerializeField] private bool bindComponents = false;

		protected void Awake()
		{
			// In case we have to bind the components, create a new dependency locator.
			IDependencyManager dependencies = bindComponents ? new DependencyManager(GlobalDependencyManager.Instance) : GlobalDependencyManager.Instance;
			DependencyUtils.Inject(gameObject, dependencies, includeChildren, bindComponents);
		}
	}
}