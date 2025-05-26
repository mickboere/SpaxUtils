using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	[NodeWidth(300)]
	public class SetRuntimeDataNode : StateComponentNodeBase
	{
		[SerializeField] private LabeledDataCollection data;
		[SerializeField] private bool overwrite;
		[SerializeField] private bool dirty;

		private RuntimeDataCollection runtimeDataCollection;

		public void InjectDependencies(RuntimeDataCollection runtimeDataCollection)
		{
			this.runtimeDataCollection = runtimeDataCollection;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			data.ApplyToRuntimeDataCollection(runtimeDataCollection, overwrite, dirty);
		}
	}
}
