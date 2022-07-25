using SpaxUtils;
using System;
using UnityEngine;

namespace SpaxUtils
{


	/// <summary>
	/// Class that catches all collider events and passes them on.
	/// Implements <see cref="MonoBehaviour"/> and <see cref="IDependency"/>.
	/// </summary>
	public class ColliderEventHandler : MonoBehaviour, IDependency
	{
		public event Action<Collision> CollisionEnterEvent;
		public event Action<Collision> CollisionExitEvent;
		public event Action<Collision> CollisionStayEvent;

		public bool Colliding { get; private set; }

		protected virtual void OnEnable()
		{
			Rigidbody rigidbody = GetComponent<Rigidbody>();
			if (rigidbody != null && rigidbody.gameObject != gameObject)
			{
				// https://docs.unity3d.com/ScriptReference/Collider.OnCollisionEnter.html:
				// "Collision events are only sent if one of the colliders also has a non-kinematic rigidbody attached."
				SpaxDebug.Error("ColliderEventHandler should be on the same object as a Rigidbody in order to receive collision events.");
			}
			else if (rigidbody == null)
			{
				rigidbody = gameObject.AddComponent<Rigidbody>();
				rigidbody.isKinematic = true;
			}
		}

		protected virtual void OnCollisionEnter(Collision collision)
		{
			Colliding = true;
			CollisionEnterEvent?.Invoke(collision);
		}

		protected virtual void OnCollisionExit(Collision collision)
		{
			Colliding = false;
			CollisionExitEvent?.Invoke(collision);
		}

		protected virtual void OnCollisionStay(Collision collision)
		{
			Colliding = true;
			CollisionStayEvent?.Invoke(collision);
		}
	}
}
