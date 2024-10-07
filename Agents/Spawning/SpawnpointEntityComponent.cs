using System.Collections;
using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(Entity))]
	public class SpawnpointEntityComponent : EntityComponentBase, ISpawnpoint
	{
		public string ID => identifier;
		public Vector3 Position => overridePoint == null ? transform.position : overridePoint.position;
		public Quaternion Rotation => overridePoint == null ? transform.rotation : overridePoint.rotation;
		public SpawnRegion Region => region;

		[SerializeField, ConstDropdown(typeof(ISpawnpointIdentifiers))] private string identifier;
		[SerializeField] private Transform overridePoint;
		[SerializeField] private SpawnRegion region;

		public void OnValidate()
		{
			if (!Application.isPlaying && isActiveAndEnabled)
			{
				Entity.Identification.ID = identifier;
			}
		}

		public void OnDrawGizmos()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = Color.red;
			Gizmos.DrawSphere(Vector3.zero, 0.1f);
			Gizmos.color = Color.blue;
			Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.5f);
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
			Gizmos.color = Color.blue;
			Gizmos.DrawCube(Vector3.forward * 0.5f, Vector3.one * 0.1f);
		}
	}
}
