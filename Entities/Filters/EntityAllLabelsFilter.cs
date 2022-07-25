namespace SpaxUtils
{
	/// <summary>
	/// <see cref="EntityFilter{T}"/> that requires the entities to have all defined labels.
	/// </summary>
	public class EntityAllLabelsFilter<T> : EntityLabelFilter<T> where T : class, IEntity
	{
		public EntityAllLabelsFilter(IEntityCollection entityCollection, params string[] tags)
			: base(entityCollection, tags)
		{
		}

		public EntityAllLabelsFilter(IEntityCollection entityCollection, string[] tags, params IEntity[] exclude)
			: base(entityCollection, tags, exclude)
		{
		}

		protected override bool TagEvaluation(T entity)
		{
			return entity.Identification.HasAll(tags);
		}
	}
}
