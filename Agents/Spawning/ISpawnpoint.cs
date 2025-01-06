using UnityEngine;

namespace SpaxUtils
{
	public interface ISpawnpoint : IIdentifiable
	{
		Vector3 Position { get; }
		Quaternion Rotation { get; }
		WorldRegion Region { get; }
	}
}
