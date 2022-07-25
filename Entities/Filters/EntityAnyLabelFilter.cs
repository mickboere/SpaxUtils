using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="EntityFilter{T}"/> that requires the entities to have any of the defined labels.
	/// </summary>
	public class EntityAnyLabelFilter<T> : EntityLabelFilter<T> where T : class, IEntity
	{
		public EntityAnyLabelFilter(IEntityCollection entityCollection, params string[] tags)
			: base(entityCollection, tags)
		{
		}

		public EntityAnyLabelFilter(IEntityCollection entityCollection, string[] tags, params IEntity[] exclude)
			: base(entityCollection, tags, exclude)
		{
		}

		protected override bool TagEvaluation(T entity)
		{
			return entity.Identification.HasAny(tags);
		}
	}
}