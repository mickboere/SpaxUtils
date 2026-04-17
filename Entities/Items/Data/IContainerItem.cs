namespace SpaxUtils
{
	public interface IContainerItem
	{
		/// <summary>
		/// The total size of this container.
		/// </summary>
		float Capacity { get; }

		/// <summary>
		/// How much this container contains, ranging between 0 and <see cref="Capacity"/>.
		/// </summary>
		float Contains { get; }

		/// <summary>
		/// How full the container is from 0-1.
		/// </summary>
		float FillAmount { get; }

		/// <summary>
		/// Whether the container is fully empty.
		/// </summary>
		bool Empty => FillAmount.Approx(0f);

		/// <summary>
		/// Whether the container is entirely full.
		/// </summary>
		bool Full => FillAmount.Approx(1f);

		/// <summary>
		/// Adds <paramref name="amount"/> to <see cref="Contains"/>.
		/// A negative amount will completely fill the container.
		/// </summary>
		/// <param name="amount">The amount with which to fill the container.</param>
		void Fill(float amount);
	}
}
