using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Blocks the auto-application of root-motion to instead allow for it to be applied manually through IE physics.
	/// </summary>
	public class BlockRootMotion : MonoBehaviour
	{
		/// <summary>
		/// By implementing this we override the Animator's RootMotion movement so that we can use that velocity data and apply it to the rigidbody ourselves.
		/// </summary>
		protected void OnAnimatorMove()
		{
		}
	}
}
