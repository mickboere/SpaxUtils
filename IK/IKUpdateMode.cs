namespace SpaxUtils
{
	/// <summary>
	/// Enum containing different update modes for IK.
	/// </summary>
	/// <seealso cref="IIKComponent"/>
	/// <seealso cref="IKComponentBase"/>
	public enum IKUpdateMode
	{
		Update,
		LateUpdate,
		FixedUpdate,
		Custom
	}
}
