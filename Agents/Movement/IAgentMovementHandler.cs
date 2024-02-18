using UnityEngine;

namespace SpaxUtils
{
	public interface IAgentMovementHandler : IEntityComponent
	{
		/// <summary>
		/// The current movement input (normalized is standard).
		/// </summary>
		Vector3 MovementInput { get; }

		/// <summary>
		/// The default movement speed in meters per second.
		/// </summary>
		float MovementSpeed { get; }

		/// <summary>
		/// Sets the default movement speed used when input is provided.
		/// </summary>
		/// <param name="speed">The movement speed in m/s.</param>
		void SetMovementSpeed(float speed);

		/// <summary>
		/// Sets the default rotation speed.
		/// </summary>
		/// <param name="speed">The rotation speed in degrees per second.</param>
		void SetRotationSpeed(float speed);

		/// <summary>
		/// Moves the <see cref="IAgent"/> in <paramref name="input"/> direction in local space, X being sideways, Y being forwards / backwards.
		/// </summary>
		/// <param name="input">Input direction in local space.</param>
		void SetMovementInput(Vector2 input);

		/// <summary>
		/// Moves the <see cref="IAgent"/> in <paramref name="input"/> direction in local space.
		/// </summary>
		/// <param name="input">Input direction in local space.</param>
		void SetMovementInput(Vector3 input);

		/// <summary>
		/// Request the <see cref="IAgent"/> to move in <paramref name="velocity"/> direction in world space.
		/// </summary>
		/// <param name="velocity">The target velocity of this agent.</param>
		void SetTargetVelocity(Vector3 velocity);

		/// <summary>
		/// Changes the axis to which the Movement Input is relative to.
		/// </summary>
		void SetInputAxis(Vector3 direction);

		/// <summary>
		/// Rotates the forward direction of the <see cref="IAgent"/> towards <paramref name="direction"/>.
		/// </summary>
		void SetTargetDirection(Vector3 direction);

		/// <summary>
		/// Force update rotation to face either <paramref name="direction"/> or the target velocity if null.
		/// </summary>
		void ForceRotation(Vector3? direction = null);
	}
}
