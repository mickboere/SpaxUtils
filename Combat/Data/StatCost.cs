using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class StatCost
	{
		public string Stat => toSubStat ? stat.SubStat(subStat) : stat;
		public float Cost => cost;
		public bool Multiply => multiply;
		public string Multiplier => multiplierToSubStat ? multiplier.SubStat(multiplierSubStat) : multiplier;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string stat;
		[SerializeField, HideInInspector] private bool toSubStat;
		[SerializeField, Conditional(nameof(toSubStat), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string subStat;
		[SerializeField] private float cost = 1f;
		[SerializeField, HideInInspector] private bool multiply;
		[SerializeField, Conditional(nameof(multiply), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string multiplier;
		[SerializeField, HideInInspector] private bool multiplierToSubStat;
		[SerializeField, Conditional(nameof(multiplierToSubStat), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string multiplierSubStat;
	}
}
