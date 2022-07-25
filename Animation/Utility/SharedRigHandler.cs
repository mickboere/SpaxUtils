using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IDependency"/> component that is able to have different <see cref="SkinnedMeshRenderer"/>s use utilize the same rig.
	/// </summary>
	[ExecuteInEditMode]
	public class SharedRigHandler : MonoBehaviour, IDependency
	{
		[SerializeField] private SkinnedMeshRenderer referenceRig;
		[SerializeField] private List<SkinnedMeshRenderer> sharers = new List<SkinnedMeshRenderer>();
		[SerializeField] private bool autoCollect;

		protected void OnValidate()
		{
			AutoCollectSharers();
			foreach (SkinnedMeshRenderer sharer in sharers)
			{
				Share(sharer);
			}
		}

		protected void Start()
		{
			AutoCollectSharers();
			foreach (SkinnedMeshRenderer sharer in sharers)
			{
				Share(sharer);
			}
		}

		/// <summary>
		/// Adds <paramref name="skinnedMeshRenderer"/> to the shared rig.
		/// </summary>
		/// <param name="skinnedMeshRenderer"></param>
		public void Share(SkinnedMeshRenderer skinnedMeshRenderer)
		{
			if (skinnedMeshRenderer == null)
			{
				SpaxDebug.Warning("Missing skinned mesh reference.", "", gameObject);
				return;
			}
			if (skinnedMeshRenderer == referenceRig)
			{
				SpaxDebug.Warning("Cannot share reference rig.", "", gameObject);
				return;
			}

			if (!sharers.Contains(skinnedMeshRenderer))
			{
				sharers.Add(skinnedMeshRenderer);
			}

			skinnedMeshRenderer.rootBone = referenceRig.rootBone;
			skinnedMeshRenderer.bones = referenceRig.bones;
		}

		/// <summary>
		/// Adds all <see cref="SkinnedMeshRenderer"/>s found in <paramref name="gameObject"/> and its children to the shared rig.
		/// </summary>
		public void Share(GameObject gameObject)
		{
			SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
			{
				Share(skinnedMeshRenderer);
			}
		}

		/// <summary>
		/// Unlists the <paramref name="sharer"/>.
		/// </summary>
		public void Remove(SkinnedMeshRenderer sharer)
		{
			if (sharers.Contains(sharer))
			{
				sharers.Remove(sharer);
			}
		}

		/// <summary>
		/// Unlists all <see cref="SkinnedMeshRenderer"/>s found in <paramref name="gameObject"/> and its children.
		/// </summary>
		public void Remove(GameObject gameObject)
		{
			SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
			{
				Remove(skinnedMeshRenderer);
			}
		}

		private void AutoCollectSharers()
		{
			if (!autoCollect)
			{
				return;
			}

			SkinnedMeshRenderer[] renderers = transform.root.GetComponentsInChildren<SkinnedMeshRenderer>();
			foreach (SkinnedMeshRenderer renderer in renderers)
			{
				if (renderer != referenceRig && !sharers.Contains(renderer))
				{
					sharers.Add(renderer);
				}
			}
		}
	}
}