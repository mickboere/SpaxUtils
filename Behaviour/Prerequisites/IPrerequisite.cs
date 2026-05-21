namespace SpaxUtils
{
	public interface IConditional
	{
		bool IsMet(IDependencyManager dependencies);
	}
}
