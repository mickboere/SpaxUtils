using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IModifier{T}"/> float implementation that supports basic modification using a <see cref="SpaxUtils.Operation"/>.
	/// </summary>
	[Serializable]
	public class FloatOperationModifier : FloatModifierBase
	{
		public override ModMethod Method =>
			method == ModMethod.Auto ?
				(operation == Operation.Min || operation == Operation.Max ? ModMethod.Absolute : ModMethod.Additive) :
				method;

		public Operation Operation => operation;
		public float Value => value;

		[SerializeField] private ModMethod method;
		[SerializeField] private Operation operation;
		[SerializeField] private float value;

		public FloatOperationModifier(ModMethod method, Operation operation, float value)
		{
			this.method = method;
			this.operation = operation;
			this.value = value;
		}

		public override float Modify(float input)
		{
			return Operate(input, Operation, Value);
		}

		/// <summary>
		/// Sets the modifier's method.
		/// </summary>
		public void SetMethod(ModMethod method)
		{
			this.method = method;
			Recalculate();
		}

		/// <summary>
		/// Sets the modifier's operation.
		/// </summary>
		public void SetOperation(Operation operation)
		{
			this.operation = operation;
			Recalculate();
		}

		/// <summary>
		/// Sets the modifier's value.
		/// </summary>
		public void SetValue(float value)
		{
			this.value = value;
			Recalculate();
		}

		/// <summary>
		/// Performs the given <paramref name="operation"/> on the <paramref name="input"/> using <paramref name="value"/>.
		/// </summary>
		/// <param name="input">The input value to run the operation on.</param>
		/// <param name="operation">The desired operation.</param>
		/// <param name="value">The operation value.</param>
		public static float Operate(float input, Operation operation, float value)
		{
			switch (operation)
			{
				case Operation.Add:
					return input + value;
				case Operation.Substract:
					return input - value;
				case Operation.Multiply:
					return input * value;
				case Operation.Divide:
					return input / value;
				case Operation.Min:
					return Mathf.Min(input, value);
				case Operation.Max:
					return Mathf.Max(input, value);
				case Operation.Power:
					return Mathf.Pow(input, value);
				case Operation.Set:
					return value;
				default:
					SpaxDebug.Error("FloatModifier: ", $"ModType [{operation}] is not supported.");
					return input;
			}
		}
	}
}
