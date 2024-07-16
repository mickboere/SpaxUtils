using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AIDataIdentifiers : ILabeledDataIdentifiers
	{
		public const string AI = "AI/";

		public const string HOSTILES = AI + "Hostiles"; // Points to a list of strings containing all entity labels and ID's this AI should be hostile to.

		public const string PARRY = AI + "Parry"; // Whether this AI should be able to parry attacks.
	}
}
