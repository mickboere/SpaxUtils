using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Pairs an <see cref="IAct.Title"/> to an <see cref="IPerformanceMove"/>.
	/// </summary>
	[Serializable]
	public class ActMovePair
	{
		public string Act => act;
		public PerformanceMove Move => move;
		public int Prio => prio;

		[SerializeField, ConstDropdown(typeof(IActConstants))] private string act;
		[SerializeField] private PerformanceMove move;
		[SerializeField] private int prio;
	}
}
