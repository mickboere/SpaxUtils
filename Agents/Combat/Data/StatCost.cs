using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class StatCost
	{
		public string Stat => toSubStat ? stat.SubStat(subStat) : stat;
		public float Cost => cost;
		public bool Required => required;

		[SerializeField, ConstDropdown(typeof(IStatIdentifiers), includeEmpty: true)] private string stat;
		[SerializeField, HideInInspector] private bool toSubStat;
		[SerializeField, Conditional(nameof(toSubStat), drawToggle: true), ConstDropdown(typeof(IStatIdentifiers), includeEmpty: true)] private string subStat;
		[SerializeField] private float cost = 1f;
		[SerializeField] private bool required = true;
	}
}
