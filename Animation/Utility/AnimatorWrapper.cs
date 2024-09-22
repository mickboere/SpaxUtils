using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// MonoBehaviour that wraps around Unity's <see cref="Animator"/> and provides additional functionality and optimizations.
	/// </summary>
	public class AnimatorWrapper : MonoBehaviour, IDependency
	{
		public event Action OnAnimatorMoveEvent;
		public event Action OnAnimatorIKEvent;

		private const string X = "X";
		private const string Y = "Y";
		private const string Z = "Z";

		/// <summary>
		/// Shared static dictionary to prevent duplicate hashing when there are multiple of the same animated entities.
		/// </summary>
		protected static Dictionary<string, int> Params = new Dictionary<string, int>();

		public Animator Animator => GetAnimator();
		public Vector3 Velocity => animator.velocity;
		public Vector3 AngularVelocity => animator.angularVelocity;
		public RuntimeAnimatorController Controller
		{
			get { return Animator.runtimeAnimatorController; }
			set { Animator.runtimeAnimatorController = value; }
		}

		[SerializeField, Tooltip("Optional")] private Animator animator;
		[SerializeField, Tooltip("Adds the BlockRootMotion component")] private bool blockRootMotionApplication;

		private Dictionary<string, int> layers = new Dictionary<string, int>();
		private Dictionary<int, Coroutine> runningBoolCoroutines = new Dictionary<int, Coroutine>();
		private List<string> cachedParameters;

		protected void Reset()
		{
			GetAnimator();
		}

		protected void Awake()
		{
			GetAnimator();
		}

		protected void OnAnimatorMove()
		{
			OnAnimatorMoveEvent?.Invoke();
		}

		protected void OnAnimatorIK()
		{
			OnAnimatorIKEvent?.Invoke();
		}

		private Animator GetAnimator()
		{
			// If animator is null, attempt to find it.

			if (animator == null)
			{
				animator = gameObject.GetComponentRelative<Animator>();
			}

			if (animator == null)
			{
				//SpaxDebug.Error("AnimatorWrapper could not find an Animator to wrap around.");
			}
			else if (Application.isPlaying)
			{
				if (blockRootMotionApplication && !animator.gameObject.GetComponent<BlockRootMotion>())
				{
					animator.gameObject.AddComponent<BlockRootMotion>();
				}
			}

			return animator;
		}

		#region Layers

		/// <summary>
		/// Retrieves the index of the layer name <paramref name="layerName"/>.
		/// </summary>
		public int GetLayerIndex(string layerName)
		{
			if (layers.ContainsKey(layerName))
			{
				return layers[layerName];
			}
			else
			{
				int index = Animator.GetLayerIndex(layerName);
				layers.Add(layerName, index);
				return index;
			}
		}

		/// <summary>
		/// Attempts to get <paramref name="layerName"/>'s index, returns false if failed.
		/// </summary>
		public bool TryGetLayerIndex(string layerName, out int index)
		{
			index = GetLayerIndex(layerName);
			bool result = index >= 0;
			if (!result)
			{
				SpaxDebug.Warning($"Layer '{layerName}' could not be found");
			}
			return result;
		}

		/// <summary>
		/// Sets the Weight of the layer named <paramref name="layerName"/> to <paramref name="weight"/>.
		/// </summary>
		public void SetLayerWeight(string layerName, float weight)
		{
			if (!TryGetLayerIndex(layerName, out int index))
			{
				return;
			}

			animator.SetLayerWeight(index, weight);
		}

		/// <summary>
		/// Returns the current weight of the layer named <paramref name="layerName"/>.
		/// </summary>
		public float GetLayerWeight(string layerName)
		{
			if (!TryGetLayerIndex(layerName, out int index))
			{
				return 0f;
			}

			return animator.GetLayerWeight(index);
		}

		#endregion Layers

		#region Params

		/// <summary>
		/// Returns a cached int hash value of <paramref name="param"/>.
		/// <para>See <see cref="Animator.StringToHash(string)"/>.</para>
		/// </summary>
		/// <param name="param">The parameter to retrieve the hash of.</param>
		/// <returns>A cached int hash value of <paramref name="param"/>.</returns>
		public static int GetParamHash(string param)
		{
			if (Params.ContainsKey(param))
			{
				return Params[param];
			}
			else
			{
				int paramHash = Animator.StringToHash(param);
				Params.Add(param, paramHash);
				return paramHash;
			}
		}

		public bool HasParameter(string param)
		{
			// Retrieve all parameter names if we haven't done so already.
			if (cachedParameters == null)
			{
				cachedParameters = new List<string>();
				foreach (AnimatorControllerParameter p in animator.parameters)
				{
					cachedParameters.Add(p.name);
				}
			}

			return cachedParameters.Contains(param);
		}

		#endregion

		#region Bools

		/// <summary>
		/// Triggers bool parameter <paramref name="param"/> for <paramref name="frames"/> amount of frames.
		/// </summary>
		/// <param name="param">The bool parameter to trigger.</param>
		/// <param name="frames">The amount of frames to keep the bool enabled for.</param>
		public void TriggerBool(string param, int frames = 1)
		{
			int paramHash = GetParamHash(param);
			if (runningBoolCoroutines.ContainsKey(paramHash))
			{
				StopCoroutine(runningBoolCoroutines[paramHash]);
			}

			// We need to add 1 frame to compensate for the Animator update loop being offset causing the bool value to never be processed.
			runningBoolCoroutines[paramHash] = StartCoroutine(TriggerBoolForFrames(paramHash, frames + 1));
		}

		/// <summary>
		/// Triggers bool parameter <paramref name="param"/> for <paramref name="seconds"/> amount of seconds.
		/// </summary>
		/// <param name="param">The bool parameter to trigger.</param>
		/// <param name="seconds">The amount of seconds to keep the bool enabled for.</param>
		public void TriggerBool(string param, float seconds)
		{
			int paramHash = GetParamHash(param);
			if (runningBoolCoroutines.ContainsKey(paramHash))
			{
				StopCoroutine(runningBoolCoroutines[paramHash]);
			}

			runningBoolCoroutines[paramHash] = StartCoroutine(TriggerBoolForSeconds(paramHash, seconds));
		}

		/// <summary>
		/// Sets bool parameter <paramref name="param"/> to value <paramref name="value"/>.
		/// </summary>
		/// <param name="param">The bool parameter to set.</param>
		/// <param name="value">The value to set the bool parameter to.</param>
		public void SetBool(string param, bool value)
		{
			int paramHash = GetParamHash(param);
			if (runningBoolCoroutines.ContainsKey(paramHash))
			{
				StopCoroutine(runningBoolCoroutines[paramHash]);
				runningBoolCoroutines.Remove(paramHash);
			}

			animator.SetBool(paramHash, value);
		}

		/// <summary>
		/// Tries to set bool parameter <paramref name="param"/> to value <paramref name="value"/>.
		/// Returns false if the parameter does not exist.
		/// </summary>
		/// <param name="param">The bool parameter to set.</param>
		/// <param name="value">The value to set the bool parameter to.</param>
		public bool TrySetBool(string param, bool value)
		{
			if (HasParameter(param))
			{
				SetBool(param, value);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Retrieves a bool parameter's value from the animator.
		/// </summary>
		/// <param name="param">The bool parameter to retrieve the value of.</param>
		/// <returns>The <see cref="bool"/> value of <paramref name="param"/>.</returns>
		public bool GetBool(string param)
		{
			int paramHash = GetParamHash(param);
			return animator.GetBool(paramHash);
		}

		#endregion Bools

		#region Floats

		/// <summary>
		/// Sets float parameter <paramref name="param"/> to value <paramref name="value"/>.
		/// </summary>
		/// <param name="param">The float parameter to set.</param>
		/// <param name="value">The value to set the float parameter to.</param>
		public void SetFloat(string param, float value)
		{
			int paramHash = GetParamHash(param);
			animator.SetFloat(paramHash, value);
		}

		/// <summary>
		/// Retrieves a float parameter's value from the animator.
		/// </summary>
		/// <param name="param">The float parameter to retrieve the value of.</param>
		/// <returns>The <see cref="float"/> value of <paramref name="param"/>.</returns>
		public float GetFloat(string param)
		{
			int paramHash = GetParamHash(param);
			return animator.GetFloat(paramHash);
		}

		#endregion

		#region Integers

		/// <summary>
		/// Sets int parameter <paramref name="param"/> to value <paramref name="value"/>.
		/// </summary>
		/// <param name="param">The int parameter to set.</param>
		/// <param name="value">The value to set the int parameter to.</param>
		public void SetInteger(string param, int value)
		{
			int paramHash = GetParamHash(param);
			animator.SetInteger(paramHash, value);
		}

		/// <summary>
		/// Retrieves an integer parameter's value from the animator.
		/// </summary>
		/// <param name="param">The int parameter to retrieve the value of.</param>
		/// <returns>The <see cref="int"/> value of <paramref name="param"/>.</returns>
		public int GetInteger(string param)
		{
			int paramHash = GetParamHash(param);
			return animator.GetInteger(paramHash);
		}

		#endregion Integers

		#region Vector3

		/// <summary>
		/// Short hand for setting <paramref name="paramNameBase"/>X, <paramref name="paramNameBase"/>Y, <paramref name="paramNameBase"/>Z.
		/// <para>For example, making <paramref name="paramNameBase"/> "Foo", this method will set the values for parameters "FooX", "FooY" and "FooZ".</para>
		/// </summary>
		/// <param name="paramNameBase">The base param name to which this method will suffix 'X', 'Y' and 'Z'.</param>
		/// <param name="value">The <see cref="Vector3"/> value to set the float parameters to.</param>
		public void SetVector3(string paramNameBase, Vector3 value)
		{
			SetFloat(paramNameBase + X, value.x);
			SetFloat(paramNameBase + Y, value.y);
			SetFloat(paramNameBase + Z, value.z);
		}

		/// <summary>
		/// Short hand for retrieving a new Vector3 of (<paramref name="paramNameBase"/>X, <paramref name="paramNameBase"/>Y, <paramref name="paramNameBase"/>Z).
		/// <para>For example, making <paramref name="paramNameBase"/> "Foo", this method will get the values for parameters "FooX", "FooY" and "FooZ".</para>
		/// </summary>
		/// <param name="paramNameBase">The base param name to which this method will suffix 'X', 'Y' and 'Z'.</param>
		/// <returns>new Vector3(<paramref name="paramNameBase"/>X, <paramref name="paramNameBase"/>Y, <paramref name="paramNameBase"/>Z).</returns>
		public Vector3 GetVector3(string paramNameBase)
		{
			return new Vector3(
				GetFloat(paramNameBase + X),
				GetFloat(paramNameBase + Y),
				GetFloat(paramNameBase + Z));
		}

		#endregion Vector3

		#region Vector2

		/// <summary>
		/// Short hand for setting <paramref name="paramNameBase"/>X, <paramref name="paramNameBase"/>Y.
		/// <para>For example, making <paramref name="paramNameBase"/> "Foo", this method will set the values for parameters "FooX" and "FooY".</para>
		/// </summary>
		/// <param name="paramNameBase">The base param name to which this method will suffix 'X' and 'Y'.</param>
		/// <param name="value">The <see cref="Vector2"/> value to set the float parameters to.</param>
		public void SetVector2(string paramNameBase, Vector2 value)
		{
			SetFloat(paramNameBase + X, value.x);
			SetFloat(paramNameBase + Y, value.y);
		}

		/// <summary>
		/// Sets the Vector2's X and Y to be the <paramref name="value"/>'s X and Z.
		/// Useful for character movement based parameters.
		/// </summary>
		public void SetVector2Horizontal(string paramNameBase, Vector3 value)
		{
			SetFloat(paramNameBase + X, value.x);
			SetFloat(paramNameBase + Y, value.z);
		}

		#endregion

		/// <summary>
		/// Returns <see cref="Transform"/> mapped to this <see cref="HumanBodyBones"/> id.
		/// </summary>
		/// <param name="bone">The <see cref="HumanBodyBones"/> id to request.</param>
		/// <returns>The <see cref="Transform"/> mapped to this <see cref="HumanBodyBones"/> id.</returns>
		public Transform GetHumanBone(HumanBodyBones bone)
		{
			return animator.GetBoneTransform(bone);
		}

		private IEnumerator TriggerBoolForFrames(int paramHash, int frames)
		{
			animator.SetBool(paramHash, true);
			for (int i = 0; i < frames; i++)
			{
				yield return null;
			}
			animator.SetBool(paramHash, false);
			runningBoolCoroutines.Remove(paramHash);
		}

		private IEnumerator TriggerBoolForSeconds(int paramHash, float seconds)
		{
			animator.SetBool(paramHash, true);
			yield return new WaitForSeconds(seconds);
			animator.SetBool(paramHash, false);
			runningBoolCoroutines.Remove(paramHash);
		}
	}
}