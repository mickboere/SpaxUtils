using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class for <see cref="AEMOI"/> behaviour assets.
	/// </summary>
	public abstract class AEMOIBehaviourAsset : BehaviourAsset
	{
		public abstract bool Valid(Vector8 motivation);
	}
}
