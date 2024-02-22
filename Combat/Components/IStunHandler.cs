namespace SpaxUtils
{
	public interface IStunHandler
	{
		bool Stunned { get; }

		void EnterStun(HitData hitData);
	}
}
