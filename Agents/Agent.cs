using SpaxUtils.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base <see cref="IAgent"/> implementation.
	/// </summary>
	public class Agent : Entity, IAgent, IDependencyProvider
	{
		/// <inheritdoc/>
		public event Action<IEntity> AttachedEntityEvent;

		/// <inheritdoc/>
		public event Action<IEntity> DetachedEntityEvent;

		/// <inheritdoc/>
		public IActor Actor { get; } = new Actor();

		/// <inheritdoc/>
		public IBrain Brain { get; private set; }

		/// <inheritdoc/>
		public IAgentBody Body { get; private set; }

		/// <inheritdoc/>
		public ITargetable Targetable { get; private set; }

		/// <inheritdoc/>
		public ITargeter Targeter { get; private set; }

		/// <inheritdoc/>
		public List<IEntity> Attachments { get; private set; } = new List<IEntity>();

		protected override string GameObjectNamePrefix => "[Agent]";

		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants))] private string state;
		[SerializeField] private StateMachineGraph brainGraph;

		private CallbackService callbackService;

		/// <inheritdoc/>
		public Dictionary<object, object> RetrieveDependencies()
		{
			var dependencies = new Dictionary<object, object>();
			dependencies.Add(typeof(Actor), Actor);
			return dependencies;
		}

		public void InjectDependencies(IAgentBody body, ITargetable targetableComponent, ITargeter targeterComponent,
			IEntity[] entities, IPerformer[] performers, CallbackService callbackService)
		{
			Body = body;
			Targetable = targetableComponent;
			Targeter = targeterComponent;
			this.callbackService = callbackService;

			foreach (IEntity entity in entities)
			{
				if (entity != this)
				{
					AttachEntity(entity);
				}
			}

			foreach (IPerformer performer in performers)
			{
				if (performer != Actor)// && performer is IEntityComponent)
				{
					Actor.AddPerformer(performer);
				}
			}

			// Create brain if there isn't one.
			if (Brain == null)
			{
				Brain = new Brain(DependencyManager, callbackService, state);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			//StartBrain();
		}

		protected override void OnDisable()
		{
			//StopBrain();
			base.OnDisable();
		}

		protected void OnDestroy()
		{
			((Actor)Actor).Dispose();
		}

		/// <inheritdoc/>
		public void AttachEntity(IEntity entity)
		{
			if (!Attachments.Contains(entity))
			{
				Attachments.Add(entity);
				AttachedEntityEvent?.Invoke(entity);
			}
		}

		/// <inheritdoc/>
		public void DetachEntity(IEntity entity)
		{
			if (Attachments.Contains(entity))
			{
				Attachments.Remove(entity);
				DetachedEntityEvent?.Invoke(entity);
			}
		}

		#region Data

		/// <inheritdoc/>
		public override void SetDataValue(string identifier, object value)
		{
			if (!RuntimeData.ContainsEntry(identifier))
			{
				foreach (IEntity attachment in Attachments)
				{
					if (attachment.RuntimeData.ContainsEntry(identifier))
					{
						attachment.RuntimeData.Set(identifier, value);
						return;
					}
				}
			}

			RuntimeData.Set(identifier, value);
		}

		/// <inheritdoc/>
		public override object GetDataValue(string identifier)
		{
			if (!RuntimeData.ContainsEntry(identifier))
			{
				foreach (IEntity attachment in Attachments)
				{
					if (attachment.RuntimeData.ContainsEntry(identifier))
					{
						return attachment.RuntimeData.Get(identifier);
					}
				}
			}

			return RuntimeData.Get(identifier);
		}

		/// <inheritdoc/>
		public override T GetDataValue<T>(string identifier)
		{
			if (!RuntimeData.ContainsEntry(identifier))
			{
				foreach (IEntity attachment in Attachments)
				{
					if (attachment.RuntimeData.ContainsEntry(identifier))
					{
						return attachment.RuntimeData.Get<T>(identifier);
					}
				}
			}

			return RuntimeData.Get<T>(identifier);
		}

		/// <inheritdoc/>
		public override EntityStat GetStat(string identifier, bool createDataIfNull = false)
		{
			// Check base entity for stat.
			EntityStat stat = base.GetStat(identifier, createDataIfNull);

			// If base does not contain requested stat, check in attachments.
			if (stat == null)
			{
				foreach (IEntity attachment in Attachments)
				{
					stat = attachment.GetStat(identifier, false);
					if (stat != null)
					{
						// Stat was found within attachment.
						break;
					}
				}
			}

			return stat;
		}

		#endregion

		/// <inheritdoc/>
		public override IEntityComponent GetEntityComponent(Type type)
		{
			// Type must implement IEntityComponent, else it can't possibly be in our list.
			if (!typeof(IEntityComponent).IsAssignableFrom(type))
			{
				SpaxDebug.Error("GetEntityComponent ", $"Type {type} is not assignable to IEntityComponent.", this);
				return null;
			}

			IEntityComponent component = Components.FirstOrDefault((e) => type.IsAssignableFrom(e.GetType()));

			// If the agent does not contain the requested component, check within its attachments.
			if (component == null)
			{
				foreach (IEntity attachment in Attachments)
				{
					if (attachment.TryGetEntityComponent(type, out component))
					{
						// Component was found within attachment.
						break;
					}
				}
			}

			return component;
		}
	}
}
