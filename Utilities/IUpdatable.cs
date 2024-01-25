namespace SpaxUtils
{
	/// <summary>
	/// Interface for classes that need to be updatable through an external updater.
	/// </summary>
	public interface IUpdatable
	{
		/// <summary>
		/// Custom update method incorporating <paramref name="delta"/> time.
		/// Named "ExUpdate" because its called Externally and naming it "Update" throws an error in Unity.
		/// </summary>
		/// <param name="delta">The delta between the last and current update.</param>
		void ExUpdate(float delta);
	}
}
