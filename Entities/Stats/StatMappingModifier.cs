namespace SpaxUtils
{
	/// <summary>
	/// Float modifier that uses another stat as mod value.
	/// </summary>
	public class StatMappingModifier : FloatModifierBase
	{
		public override ModMethod Method => Mapping.ModMethod;
		public StatMapping Mapping { get; private set; }

		private CompositeFloatBase inputStat;

		public StatMappingModifier(StatMapping mapping, CompositeFloatBase inputStat)
		{
			this.Mapping = mapping;
			this.inputStat = inputStat;

			inputStat.CompositeChangedEvent += OnInputStatChanged;
		}

		public override void Dispose()
		{
			if (inputStat != null)
			{
				inputStat.CompositeChangedEvent -= OnInputStatChanged;
			}

			base.Dispose();
		}

		public override float Modify(float input)
		{
			return FloatOperationModifier.Operate(input, Mapping.Operation, Mapping.GetMappedValue(inputStat.Value));
		}

		private void OnInputStatChanged(CompositeFloatBase composite)
		{
			OnModChanged();
		}
	}
}
