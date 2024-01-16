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
		public float Buffer { get; }
		public T Value { get; }

		public Act(string title, T value, bool interuptable = false, float buffer = DEFAULT_BUFFER)
		{
			Title = title;
			Value = value;
			Interuptable = interuptable;
			Buffer = buffer;
		}

		public override string ToString()
		{
			return $"Act(\"{Title}\" : \"{Value}\", interuptable={Interuptable}, buffer={Buffer})";
		}
	}
}
