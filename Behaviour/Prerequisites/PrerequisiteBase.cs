using UnityEngine;

namespace SpaxUtils
{
	public abstract class PrerequisiteBase : ScriptableObject, IPrerequisite
	{
        public abstract bool IsMet(IDependencyManager dependencies);
	}
}
