using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// <see cref="GraphAsset"/> implementation for state machine assets.
	/// </summary>
	[CreateAssetMenu(fileName = "StateMachineGraph", menuName = "StateMachine/StateMachineGraph")]
	public class StateMachineGraph : GraphAsset, IBindingKeyProvider
	{
		public object BindingKey => GetInstanceID();
	}
}
