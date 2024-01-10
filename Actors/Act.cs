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
		public T Value { get; }
		public float Buffer { get; }

		public Act(string title, T value, float buffer = DEFAULT_BUFFER)
		{
			Title = title;
			Value = value;
			Buffer = buffer;
		}

		public override string ToString()
		{
			return $"Act(\"{Title}\" : {Value})";
		}
	}
}
