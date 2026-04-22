using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	[NodeWidth(200)]
	public class IsAtPOIRuleNode : RuleNodeBase
	{
		public override bool Valid =>
			(poiHandler.IsOccupying &&
				(labels.Length == 0 || poiHandler.CurrentPOI.Entity.Identification.HasAll(labels)))
			!= invert;

		[SerializeField, NodeInput] protected Connections.Rule ruleConnection;
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private string[] labels;
		[SerializeField] private bool invert;

		private POIHandler poiHandler;

		public void InjectDependencies(POIHandler poiHandler)
		{
			this.poiHandler = poiHandler;
		}
	}
}
