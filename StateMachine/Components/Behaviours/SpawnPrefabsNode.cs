using UnityEngine;

namespace SpaxUtils.StateMachine
{
	[NodeWidth(300)]
	public class SpawnPrefabsNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField] private GameObject[] prefabs;

		private IDependencyManager dependencies;

		public void InjectDependencies(IDependencyManager dependencies)
		{
			this.dependencies = dependencies;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			foreach (GameObject prefab in prefabs)
			{
				DependencyUtils.InstantiateAndInject(prefab, dependencies);
			}
		}
	}
}