﻿using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation for <see cref="IEntityComponent"/>.
	/// </summary>
	public abstract class EntityComponentBase : MonoBehaviour, IEntityComponent
	{
		public GameObject GameObject => Entity.GameObject;
		public Transform Transform => Entity.GameObject.transform;

		public IEntity Entity
		{
			get
			{
				if (entity == null)
				{
					entity = gameObject.GetComponentInParent<IEntity>();
				}

				return entity;
			}
		}

		private IEntity entity;

		public void InjectDependencies(IEntity entity)
		{
			this.entity = entity;
		}
	}
}