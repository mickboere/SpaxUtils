using UnityEngine;

namespace SpaxUtils
{
	public class EnemyData
	{
		public IAgent Agent;
		public float LastSeen;
		public Vector3 Direction;
		public float Distance;
		public float Resentment;
		public float Reach;
		public float Threat;
		public float Advantage;
		public float Disadvantage;
		public float Oppurtunity;
		public float Projection;

		public EnemyData(IAgent agent)
		{
			Agent = agent;
		}
	}
}
