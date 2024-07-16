using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class for <see cref="AEMOI"/> behaviour assets.
	/// </summary>
	public abstract class AEMOIBehaviourAsset : BehaviourAsset, IAEMOIBehaviour
	{
		public virtual bool Interuptable { get; protected set; } = true;

		public virtual IEntity Target { get; protected set; }

		public abstract bool Valid(Vector8 motivation, IEntity target, out float distance);

		public abstract void Initialize(IEntity target);
	}

	public interface IAEMOIBehaviour : IBehaviour
	{
		bool Interuptable { get; }

		IEntity Target { get; }

		bool Valid(Vector8 motivation, IEntity target, out float distance);

		void Initialize(IEntity target);
	}
}
