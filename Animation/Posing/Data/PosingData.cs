using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class for <see cref="ScriptableObject"/>s that are able to return <see cref="IPoserInstructions"/> through evaluation.
	/// </summary>
	public abstract class PosingData : ScriptableObject
	{
		public abstract IPoserInstructions GetInstructions(float time);
		public abstract IPoserInstructions GetInstructions(float time, Vector3 position);
	}
}
