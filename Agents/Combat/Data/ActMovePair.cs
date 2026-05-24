using System;
using System.Collections.Generic;
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
		public IReadOnlyList<IConditional> Conditions => conditions?.AsReadOnly() ?? Array.Empty<IConditional>();

		[SerializeField, ConstDropdown(typeof(IActIdentifiers))] private string act;
		[SerializeField] private PerformanceMove move;
		[SerializeField] private int prio;
		[SerializeField] private ConditionalList conditions;
	}
}
