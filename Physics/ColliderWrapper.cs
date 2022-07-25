using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Wraps around a <see cref="UnityEngine.Collider"/>, exposing some generic info.
	/// Implements <see cref="ColliderEventHandler"/> and <see cref="IDependency"/>.
	/// </summary>
	public class ColliderWrapper : ColliderEventHandler, IBindingKeyProvider
	{
		public object BindingKey => this.GetPath();

		public Collider Collider => collider;

		public float Radius
		{
			get
			{
				switch (Collider)
				{
					case null:
						Debug.LogError("Collider is null.", gameObject);
						return 0f;
					case SphereCollider sphereCollider:
						return sphereCollider.radius;
					case CapsuleCollider capsuleCollider:
						return capsuleCollider.radius;
					default:
						Debug.LogError("Requesting radius for collider, but this collider does not support a radius.", gameObject);
						return 0f;
				}
			}
		}

		public Vector3 Center
		{
			get
			{
				switch (Collider)
				{
					case null:
						Debug.LogError("Collider is null.", gameObject);
						return Vector3.zero;
					case SphereCollider sphereCollider:
						return sphereCollider.center;
					case CapsuleCollider capsuleCollider:
						return capsuleCollider.center;
					case BoxCollider boxCollider:
						return boxCollider.center;
					case MeshCollider _:
					default:
						return Vector3.zero;
				}
			}
		}

		public Vector3 Position => transform.position + transform.rotation * Center;

		new private Collider collider;

		protected override void OnEnable()
		{
			base.OnEnable();

			Collider[] colliders = GetComponents<Collider>();
			if (colliders.Length != 1)
			{
				SpaxDebug.Error("ColliderWrapper requires a single collider on its GameObject.", $"Object '{gameObject.name}' contains <b>[{colliders.Length}]</b>.", gameObject);
			}
			else
			{
				collider = colliders[0];
			}
		}
	}
}
