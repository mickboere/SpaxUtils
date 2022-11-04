using SpaxUtils;
using UnityEngine;
using SpaxUtils.StateMachine;
using System;
using System.Linq;

namespace SpiritAxis
{
	/// <summary>
	/// <see cref="IRule"/> <see cref="RuleNodeBase"/> implementation that is valid when the configured nodes have been entered before.
	/// </summary>
	[NodeWidth(300)]
	public class HasEnteredNodeRuleNode : RuleNodeBase
	{
		public override string UserFacingName => "Has Entered Node Rule";
		public override bool Valid => valid;
		public override float Validity => nodes.Length;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule inConnection;
		[SerializeField] private StateNodeBase[] nodes;
		[SerializeField] private bool invert;

		private IHistory history;
		private bool valid;
		private string[] nodeIDs;

		public override void OnPrepare()
		{
			base.OnPrepare();
			nodeIDs = nodes.Select((node) => node.ID).ToArray();
		}

		public void InjectDependencies(IHistory history)
		{
			this.history = history;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			// TODO: Reimplement history.
			SpaxDebug.Error("Histsory not implemented yet.");

			history.AddedToHistoryEvent += OnAddedToHistoryEvent;
			UpdateValidity();
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			history.AddedToHistoryEvent -= OnAddedToHistoryEvent;
		}

		private void UpdateValidity()
		{
			valid = history.Contains(nodeIDs) != invert;
		}

		private void OnAddedToHistoryEvent(string nodeID)
		{
			if (nodeIDs.Contains(nodeID))
			{
				UpdateValidity();
			}
		}
	}
}