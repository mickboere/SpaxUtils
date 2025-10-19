namespace SpaxUtils
{
	public interface IPrerequisite
	{
        bool IsMet(IDependencyManager dependencies);
	}
}
