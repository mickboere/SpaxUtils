namespace SpaxUtils
{
	/// <summary>
	/// <see cref="EntityFilter{T}"/> implementation that allows for easy creation of entity tag filters.
	/// Has the added benefit of re-evaluating everytime an entity's tags have changed.
	/// </summary>
	public abstract class EntityLabelFilter<T> : EntityFilter<T> where T : class, IEntity
	{
		protected string[] tags;

		public EntityLabelFilter(IEntityCollection entityCollection, string[] tags, params IEntity[] exclude)
			: base(entityCollection, exclude)
		{
			this.tags = tags;
			evaluate = TagEvaluation;
		}

		protected abstract bool TagEvaluation(T entity);

		protected override void AddEntity(T entity)
		{
			entity.Identification.LabelsChangedEvent += OnEntityTagsChanged;
			base.AddEntity(entity);
		}

		protected override void RemoveEntity(T entity)
		{
			entity.Identification.LabelsChangedEvent -= OnEntityTagsChanged;
			base.RemoveEntity(entity);
		}

		protected virtual void OnEntityTagsChanged(IIdentification identification)
		{
			if (identification.Entity is T cast && !evaluate(cast))
			{
				RemoveEntity(cast);
			}
		}

		public override void Dispose()
		{
			foreach (T entity in Entities)
			{
				entity.Identification.LabelsChangedEvent -= OnEntityTagsChanged;
			}
			base.Dispose();
		}
	}
}
