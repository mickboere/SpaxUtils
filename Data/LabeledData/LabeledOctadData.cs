using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledOctadData
	{
		public StatOctadAsset StatOctadAsset => octad;
		public StatOctad StatOctad => octad.StatOctad;
		public Vector8 Vector8 => values;

		[SerializeField, Expandable] private StatOctadAsset octad;
		[SerializeField] private Vector8 values;

		public void Apply(RuntimeDataCollection runtimeDataCollection, bool overwrite, bool dirty)
		{
			for (int i = 0; i < 8; i++)
			{
				string id = StatOctad.GetIdentifier(i);
				if (overwrite || runtimeDataCollection.GetEntry(id) == null)
				{
					runtimeDataCollection.SetValue(id, Vector8[i], true, dirty);
				}
			}
		}
	}
}
