using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Simple <see cref="IStatModConfig"/> with basic parameters.
	/// <seealso cref="StatModifier"/>.
	/// </summary>
	[Serializable]
	public class StatModConfig : IStatModConfig
	{
		public ModMethod Method => method;
		public Operation Operation => operation;

		[SerializeField] private ModMethod method = ModMethod.Base;
		[SerializeField] private Operation operation = Operation.Set;
		[SerializeField] private float scale = 1f;

		public StatModConfig(ModMethod method, Operation operation, float scale = 1f)
		{
			this.method = method;
			this.operation = operation;
			this.scale = scale;
		}

		public float GetModifierValue(float modStat)
		{
			return modStat * scale;
		}
	}
}
