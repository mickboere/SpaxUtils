using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using XNode;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// Most basic class of statemachine node, contains generic node related methods.
	/// </summary>
	public abstract class StateMachineNodeBase : Node, IStateComponent
	{
		/// <inheritdoc/>
		public virtual int ExecutionOrder => 0;

		/// <inheritdoc/>
		public bool InjectStateDependencies => true;

		/// <summary>
		/// Name string used in the editor.
		/// </summary>
		public virtual string UserFacingName => string.Join(" ", new List<string>(SplitCamelCase(GetType().Name)).Where((n) => n != "Node"));

		public override object GetValue(NodePort port)
		{
			return null;
		}

		protected override void Init()
		{
			base.Init();
			name = UserFacingName;
		}

		#region Callbacks

		/// <inheritdoc/>
		public virtual void OnPrepare() { }

		/// <inheritdoc/>
		public virtual void OnEnteringState(Action callback)
		{
			callback();
		}

		/// <inheritdoc/>
		public virtual void OnStateEntered() { }

		/// <inheritdoc/>
		public virtual void OnExitingState(Action callback)
		{
			callback();
		}

		/// <inheritdoc/>
		public virtual void OnStateExit() { }

		/// <inheritdoc/>
		public virtual void OnUpdate() { }

		#endregion

		/// <inheritdoc/>
		public List<IStateComponent> GetComponents()
		{
			return NodeExtensions.GetAllOutputNodes(this).Cast<IStateComponent>().Where((c) => c is not IState).ToList();
		}

		#region Connections

		/// <summary>
		/// Maps to <see cref="NodeExtensions.GetInputNodes{T}(Node, string){T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public List<T> GetInputNodes<T>(string port) where T : class
		{
			return NodeExtensions.GetInputNodes<T>(this, port);
		}

		/// <summary>
		/// Maps to <see cref="NodeExtensions.GetInputNode{T}(Node, string){T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public T GetInputNode<T>(string port) where T : class
		{
			return NodeExtensions.GetInputNode<T>(this, port);
		}

		/// <summary>
		/// Maps to <see cref="NodeExtensions.GetOutputNodes{T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public List<T> GetOutputNodes<T>(string port) where T : class
		{
			return NodeExtensions.GetOutputNodes<T>(this, port);
		}

		/// <summary>
		/// Maps to <see cref="NodeExtensions.GetOutputNode{T}(StateMachineNodeBase, string)"/>.
		/// </summary>
		public T GetOutputNode<T>(string port) where T : class
		{
			return NodeExtensions.GetOutputNode<T>(this, port);
		}

		#endregion

		/// <summary>
		/// Split <paramref name="source"/> into camel cased words.
		/// </summary>
		protected string[] SplitCamelCase(string source)
		{
			return Regex.Split(source, @"(?<!^)(?=[A-Z])");
		}
	}
}
