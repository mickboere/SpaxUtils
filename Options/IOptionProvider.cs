namespace SpaxUtils
{
	public interface IOptionProvider<T>
	{
		void RequestOptions(IRequestOptionsMsg<T> request);
	}
}
