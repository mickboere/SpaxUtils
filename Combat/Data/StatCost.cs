using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class StatCost
	{
		public string Stat => stat;
		public float Cost => cost;
		public bool Multiply => multiply;
		public string Multiplier => multiplier;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string stat;
		[SerializeField] private float cost = 1f;
		[SerializeField, HideInInspector] private bool multiply;
		[SerializeField, Conditional(nameof(multiply), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string multiplier;
	}
}
