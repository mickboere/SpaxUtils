using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class StatCost
	{
		public string Stat => stat;
		public float Cost => cost;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string stat;
		[SerializeField] private float cost = 1f;
	}
}
