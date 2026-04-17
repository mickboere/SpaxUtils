using UnityEngine;

namespace SpaxUtils
{
	public interface IAgentMovementHandler : IEntityComponent
	{
		/// <summary>
		/// The forward axis in relation to the <see cref="InputRaw"/>.
		/// </summary>
		Vector3 InputAxis { get; set; }

		/// <summary>
		/// The current movement input. Can be set externally at any point to alter the Agent's desired movement.
		/// </summary>
		Vector3 InputRaw { get; set; }

		/// <summary>
		/// The current smoothed-out movement input. Get-only, updated once every fixed update using <see cref="InputRaw"/>.
		/// </summary>
		Vector3 InputSmooth { get; set; }

		/// <summary>
		/// The agent's desired forward direction.
		/// </summary>
		Vector3 TargetDirection { get; set; }

		/// <summary>
		/// Whether the agent's rotation should be locked, preventing it from automatically pointing in the movement direction.
		/// </summary>
		bool LockRotation { get; set; }

		/// <summary>
		/// Whether to automatically apply the default movement behaviour.
		/// </summary>
		bool AutoUpdateMovement { get; set; }

		/// <summary>
		/// Whether to automatically apply the default rotation behaviour.
		/// </summary>
		bool AutoUpdateRotation { get; set; }

		/// <summary>
		/// Movement speed (m/s) when input length is exceeds the input deadzone.
		/// </summary>
		float MinSpeed { get; set; }

		/// <summary>
		/// Movement speed (m/s) when input length is equal to 0.5.
		/// </summary>
		float HalfSpeed { get; set; }

		/// <summary>
		/// Movement speed (m/s) when input length is equal to 1.
		/// </summary>
		float FullSpeed { get; set; }

		/// <summary>
		/// Update agent's velocity to match target velocity.
		/// </summary>
		/// <param name="delta">The delta time between movement updates.</param>
		/// <param name="targetVelocity">The desired velocity to try and reach each update (<see cref="RigidbodyWrapper.TargetVelocity"/> if null).</param>
		/// <param name="ignoreControl">Whether to ignore <see cref="RigidbodyWrapper.Control"/>.</param>
		void UpdateMovement(float delta, Vector3? targetVelocity = null, bool ignoreControl = false);

		/// <summary>
		/// Update rotation to face target direction.
		/// </summary>
		/// <param name="delta">The delta time between rotation updates.</param>
		/// <param name="direction">The desired facing diration (<see cref="RigidbodyWrapper.TargetVelocity"/> if null).</param>
		/// <param name="ignoreControl">Whether to ignore <see cref="RigidbodyWrapper.Control"/>.</param>
		void UpdateRotation(float delta, Vector3? direction = null, bool ignoreControl = false);

		/// <summary>
		/// Force update rotation to directly face either <paramref name="direction"/> or else the RigidbodyWrapper's target velocity.
		/// </summary>
		/// <param name="direction">Overridable target direction (picks rigidbody target velocity if null).</param>
		/// <param name="maxTurn">Maximum turn amount (0..1) where 1 is 180 degrees.</param>
		void ForceRotation(Vector3? direction = null, float maxTurn = 1f);

		/// <summary>
		/// Calculates the resulting output speed for an input length of <paramref name="inputLength"/>.
		/// </summary>
		float CalculateSpeed(float inputLength);
	}
}
