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

		[SerializeField, ConstDropdown(typeof(SpawnpointIdentifiers))] private string identifier;
		[SerializeField] private Transform overridePoint;
		[SerializeField] private SpawnRegion region;

		public void OnValidate()
		{
			Entity.Identification.ID = identifier;
		}
	}

	public interface ISpawnpoint : IIdentifiable
	{
		Vector3 Position { get; }
		Quaternion Rotation { get; }
		SpawnRegion Region { get; }
	}
}
