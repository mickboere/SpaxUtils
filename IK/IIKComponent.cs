using UnityEngine;

namespace SpaxUtils
{
	public interface IIKComponent : IEntityComponent
	{

		void AddInfluencer(object caller, string ikChain, Transform target, float positionWeight, float rotationWeight);

		void AddInfluencer(object caller, string ikChain, Vector3 position, float positionWeight, Quaternion rotation, float rotationWeight);

		void RemoveInfluencer(object caller, string ikChain);
	}
}
