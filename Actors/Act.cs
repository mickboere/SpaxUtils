namespace SpaxUtils
{
	/// <summary>
	/// Generic <see cref="IAct"/> implementation holding a single value datatype.
	/// </summary>
	/// <typeparam name="T">The data value type.</typeparam>
	public struct Act<T> : IAct
	{
		public const float DEFAULT_BUFFER = 0.15f;

		public string Title { get; }
		public bool Interuptable { get; }
		public bool Interuptor { get; }
		public float Buffer { get; }
		public T Value { get; }

		public Act(string title, T value, bool interuptable = false, bool interuptor = true, float buffer = DEFAULT_BUFFER)
		{
			Title = title;
			Value = value;
			Interuptable = interuptable;
			Interuptor = interuptor;
			Buffer = buffer;
		}

		public Act(IAct act, T value)
		{
			Title = act.Title;
			Value = value;
			Interuptable = act.Interuptable;
			Interuptor = act.Interuptor;
			Buffer = act.Buffer;
		}

		public override string ToString()
		{
			return $"Act(\"{Title}\" : \"{Value}\", interuptable={Interuptable}, interuptor={Interuptor}, buffer={Buffer})";
		}
	}
}
