using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class StatOctadMapping
	{
		[SerializeField, Expandable] private StatOctadAsset fromStatOctad;
		[SerializeField, Expandable] private StatOctadAsset toStatOctad;

		[SerializeField] private ModMethod modMethod = ModMethod.Base;
		[SerializeField] private Operation operation = Operation.Set;

		public StatMapping[] GetMappings()
		{
			StatMapping[] mappings = new StatMapping[8];
			for (int i = 0; i < 8; i++)
			{
				string fromStat = fromStatOctad.StatOctad.GetIdentifier(i);
				string toStat = toStatOctad.StatOctad.GetIdentifier(i);
				mappings[i] = new StatMapping(fromStat, false, toStat,
					formula: FormulaType.Linear,
					modMethod: modMethod,
					operation: operation);
			}
			return mappings;
		}
	}
}