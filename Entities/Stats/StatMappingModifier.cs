namespace SpaxUtils
{
	/// <summary>
	/// Float modifier that uses another stat as mod value.
	/// </summary>
	public class StatMappingModifier : FloatModifierBase
	{
		public override ModMethod Method => mapping.ModMethod;

		private StatMapping mapping;
		private CompositeFloatBase inputStat;

		public StatMappingModifier(StatMapping mapping, CompositeFloatBase inputStat)
		{
			this.mapping = mapping;
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
			return FloatOperationModifier.Operate(input, mapping.Operation, mapping.GetMappedValue(inputStat.Value));
		}

		private void OnInputStatChanged(CompositeFloatBase composite)
		{
			OnModChanged();
		}
	}
}
