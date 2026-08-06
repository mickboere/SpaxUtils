namespace SpaxUtils
{
	/// <summary>
	/// How a <see cref="TimelineMarker"/>'s end is defined.
	/// </summary>
	public enum MarkerEnd
	{
		/// <summary>An instant; fires when the playhead crosses it.</summary>
		Point = 0,
		/// <summary>A region lasting a fixed amount of seconds.</summary>
		Duration = 1,
		/// <summary>A region ending where the next marker of a referenced identifier starts.</summary>
		Marker = 2
	}
}
