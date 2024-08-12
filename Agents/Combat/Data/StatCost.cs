using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class StatCost
	{
		public string Stat => toSubStat ? stat.SubStat(subStat) : stat;
		public float Cost => cost;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string stat;
		[SerializeField, HideInInspector] private bool toSubStat;
		[SerializeField, Conditional(nameof(toSubStat), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string subStat;
		[SerializeField] private float cost = 1f;
	}
}
