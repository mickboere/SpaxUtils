namespace SpaxUtils
{
	/// <summary>
	/// Float modifier that uses a <see cref="RuntimeDataEntry"/> as mod value.
	/// </summary>
	public class DataStatMappingModifier : FloatModifierBase
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

		public override void Dispose()
		{
			if (Data != null)
			{
				Data.ValueChangedEvent -= OnValueChangedEvent;
			}

			base.Dispose();
		}

		public override float Modify(float input)
		{
			return FloatOperationModifier.Operate(input, Mapping.Operation, Mapping.GetModifierValue((float)Data.Value));
		}

		private void OnValueChangedEvent(object value)
		{
			OnModChanged();
		}
	}
}
