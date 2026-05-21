using UnityEngine;

namespace SpaxUtils
{
	public abstract class PrerequisiteBase : ScriptableObject, IConditional
	{
		public abstract bool IsMet(IDependencyManager dependencies);
	}
}
