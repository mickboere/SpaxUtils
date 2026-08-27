using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for components managing an entity's IK chains.
	/// </summary>
	public interface IIKComponent : IEntityComponent
	{
		void AddInfluencer(object caller, string chain, int priority, Transform target, float positionWeight, float rotationWeight);

		void AddInfluencer(object caller, string chain, int priority, Vector3 position, float positionWeight, Quaternion rotation, float rotationWeight);

		void RemoveInfluencer(object caller, string chain);

		/// <summary>
		/// Influences a chain's bend goal — where the elbow or knee points. Blends from its authored pose.
		/// </summary>
		void AddHintInfluencer(object caller, string chain, int priority, Vector3 position, float weight);

		void RemoveHintInfluencer(object caller, string chain);

		/// <summary>
		/// A chain's neutral bend goal, in world space. What an influencer fading out must arrive at, or
		/// the goal travels there on its own while the constraint is still acting.
		/// </summary>
		bool TryGetHintRest(string chain, out Vector3 position);

		/// <summary>
		/// Applies the influencers to the IK.
		/// </summary>
		/// <param name="chain">The chain to apply.</param>
		void ApplyInfluencer(string chain);
	}
}
