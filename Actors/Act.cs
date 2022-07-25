namespace SpaxUtils
{
	/// <summary>
	/// Generic <see cref="IAct"/> implementation holding a single value datatype.
	/// </summary>
	/// <typeparam name="T">The data value type.</typeparam>
	public struct Act<T> : IAct
	{
		public string Title { get; }
		public T Value { get; }

		public Act(string title, T value)
		{
			Title = title;
			Value = value;
		}

		public override string ToString()
		{
			return $"Act(\"{Title}\" : {Value})";
		}
	}
}
