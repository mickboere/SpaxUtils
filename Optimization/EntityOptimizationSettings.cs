using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(EntityOptimizationSettings), menuName = "ScriptableObjects/" + nameof(EntityOptimizationSettings))]
	public class EntityOptimizationSettings : ScriptableObject, IService
	{
		public int EntityOptimizationInterval = 1000;
		public float TopPriorityRange = 12.5f;
		public float HighPriorityRange = 25f;
		public float MediumPriorityRange = 50f;
		public float LowPriorityRange = 100f;

		public PriorityLevel GetPriorityBySqrDistance(float sqrDistance)
		{
			if (sqrDistance < TopPriorityRange * TopPriorityRange)
			{
				return PriorityLevel.Top;
			}
			else if (sqrDistance < HighPriorityRange * HighPriorityRange)
			{
				return PriorityLevel.High;
			}
			else if (sqrDistance < MediumPriorityRange * MediumPriorityRange)
			{
				return PriorityLevel.Medium;
			}
			else if (sqrDistance < LowPriorityRange * LowPriorityRange)
			{
				return PriorityLevel.Low;
			}
			else
			{
				return PriorityLevel.Culled;
			}
		}

		public PriorityLevel GetPriorityByDistance(float distance)
		{
			if (distance < TopPriorityRange)
			{
				return PriorityLevel.Top;
			}
			else if (distance < HighPriorityRange)
			{
				return PriorityLevel.High;
			}
			else if (distance < MediumPriorityRange)
			{
				return PriorityLevel.Medium;
			}
			else if (distance < LowPriorityRange)
			{
				return PriorityLevel.Low;
			}
			else
			{
				return PriorityLevel.Culled;
			}
		}
	}
}
