using System;

namespace SpaxUtils
{
	/// <summary>
	/// Float modifier that uses a <see cref="RuntimeDataEntry"/> as mod value.
	/// </summary>
	public class DataStatMappingModifier : FloatModifierBase, IDisposable
	{
		public override ModMethod Method => Mapping.Method;
		public StatMapping Mapping { get; private set; }
		public RuntimeDataEntry Data { get; private set; }

		public DataStatMappingModifier(StatMapping mapping, RuntimeDataEntry data)
		{
			Mapping = mapping;
			Data = data;

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
			return FloatOperationModifier.Operate(input, Mapping.Operation, Mapping.GetModifierValue((float)Data.Value));
		}

		private void OnValueChangedEvent(object value)
		{
			Dirty = true;
		}
	}
}
