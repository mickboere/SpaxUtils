using UnityEngine;

namespace SpaxUtils
{
	public struct Spawnpoint : ISpawnpoint
	{
		public string ID { get; }
		public Vector3 Position { get; }
		public Quaternion Rotation { get; }
		public WorldRegion Region { get; }

		public Spawnpoint(string id, Vector3 position, Quaternion rotation, WorldRegion region)
		{
			ID = id;
			Position = position;
			Rotation = rotation;
			Region = region;
		}
	}
}
