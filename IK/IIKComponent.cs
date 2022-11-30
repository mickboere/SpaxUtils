using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for components managing an entity's IK chains.
	/// </summary>
	public interface IIKComponent : IEntityComponent
	{
		void AddInfluencer(object caller, string chain, Transform target, float positionWeight, float rotationWeight);

		void AddInfluencer(object caller, string chain, Vector3 position, float positionWeight, Quaternion rotation, float rotationWeight);

		void RemoveInfluencer(object caller, string chain);

		/// <summary>
		/// Applies the influencers to the IK.
		/// </summary>
		/// <param name="chain">The chain to apply.</param>
		void ApplyInfluencer(string chain);
	}
}
