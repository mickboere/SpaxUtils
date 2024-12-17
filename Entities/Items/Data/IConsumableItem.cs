using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public interface IConsumableItem
	{
		/// <summary>
		/// Consumes this consumable for <paramref name="amount"/>%.
		/// </summary>
		/// <param name="amount">The total percentage of this consumable to consume.</param>
		void Consume(float amount = 1f);
	}
}
