using System;

namespace SpaxUtils
{
	/// <summary>
	/// Float modifier that uses a <see cref="RuntimeDataEntry"/> as mod value.
	/// </summary>
	public class DataStatMappingModifier : FloatModifierBase, IDisposable
	{
		public override ModMethod Method { get; }
		public RuntimeDataEntry Data { get; }
		public Operation Operation { get; }
		public Func<float> GetModValueFunc { get; }

		public DataStatMappingModifier(RuntimeDataEntry data, ModMethod method, Operation operation, Func<float> getModValueFunc)
		{
			Data = data;
			Method = method;
			Operation = operation;
			GetModValueFunc = getModValueFunc;

			data.ValueChangedEvent += OnValueChangedEvent;
		}

		public DataStatMappingModifier(IStatModConfig config, RuntimeDataEntry data)
		{
			Data = data;
			Method = config.Method;
			Operation = config.Operation;
			GetModValueFunc = delegate ()
			{
				return config.GetModifierValue((float)data.Value);
			};

			data.ValueChangedEvent += OnValueChangedEvent;
		}

		public void Dispose()
		{
			if (Data != null)
			{
				Data.ValueChangedEvent -= OnValueChangedEvent;
			}
		}

		public override float Modify(float input)
		{
			return FloatOperationModifier.Operate(input, Operation, GetModValueFunc());
		}

		private void OnValueChangedEvent(object value)
		{
			Dirty = true;
		}
	}
}
