namespace SpaxUtils
{
	public class FlagData
	{
		/// <summary>
		/// The ID of the object that set this flag.
		/// </summary>
		public string Owner { get; }

		/// <summary>
		/// The type of time used for determining this flag's expiration.
		/// </summary>
		public TimeType TimeType { get; }

		/// <summary>
		/// The time this flag was set.
		/// </summary>
		public float Time { get; }

		/// <summary>
		/// < 0 = never
		/// 0 = immediately
		/// > 0 = total expiration time in seconds.
		/// </summary>
		public float Expiration { get; }

		public FlagData(string owner, TimeType timeType, float time, float expiration)
		{
			Owner = owner;
			TimeType = timeType;
			Time = time;
			Expiration = expiration;
		}
	}
}