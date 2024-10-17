using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component that handles the entity's appearance by:
	/// - Ensuring all skinned renderers utilize the same rig;
	/// - Ensuring all cloth renderers collide with the rig's colliders;
	/// </summary>
	public class EntityAppearanceHandler : EntityComponentMono
	{
		[Serializable]
		public class BodyPart
		{
			public string Location => location;
			public SkinnedMeshRenderer Skin => skin;

			[SerializeField, ConstDropdown(typeof(IBodyLocations))] private string location;
			[SerializeField] private SkinnedMeshRenderer skin;
		}

		[SerializeField] private Transform skeletonRoot;
		[SerializeField] private SkinnedMeshRenderer reference;
		[SerializeField] private List<BodyPart> bodyParts;
		[SerializeField] private List<SkinnedMeshRenderer> apparel = new List<SkinnedMeshRenderer>();
		[SerializeField] private bool autoCollectApparel;

		private Dictionary<string, SkinnedMeshRenderer> body;
		private CapsuleCollider[] capsuleColliders;
		private ClothSphereColliderPair[] sphereColliders;
		private Dictionary<SkinnedMeshRenderer, IEntityApparel> coverers = new Dictionary<SkinnedMeshRenderer, IEntityApparel>();

		protected void OnValidate()
		{
			Initialize();
		}

		protected void Start()
		{
			Initialize();
		}

		private void Initialize()
		{
			// Collect bodyparts to map out base body skin locations.
			body = new Dictionary<string, SkinnedMeshRenderer>();
			foreach (BodyPart item in bodyParts)
			{
				body[item.Location] = item.Skin;
			}

			// Gather skeleton colliders for cloth renderers.
			capsuleColliders = skeletonRoot.GetComponentsInChildren<CapsuleCollider>();
			sphereColliders = skeletonRoot.GetComponentsInChildren<SphereCollider>().Select(c => new ClothSphereColliderPair(c)).ToArray();

			// If auto collect, collect all skin-sharers.
			if (autoCollectApparel)
			{
				AutoCollectApparel();
			}

#if UNITY_EDITOR
			// Make sure dev didn't add reference skin or body parts to apparel.
			if (!Application.isPlaying)
			{
				CleanApparel();
			}
#endif

			// Apply rig data from reference skin to all skinned renderers.
			foreach (BodyPart bodyPart in bodyParts)
			{
				if (bodyPart.Skin != null)
				{
					bodyPart.Skin.gameObject.SetActive(true);
					ApplyData(bodyPart.Skin);
				}
			}
			foreach (SkinnedMeshRenderer sharer in apparel)
			{
				if (sharer != null)
				{
					ConfigureApparel(sharer);
					ApplyData(sharer);
				}
			}
		}

		/// <summary>
		/// Adds all <see cref="SkinnedMeshRenderer"/>s found in <paramref name="gameObject"/> and its children to the shared rig.
		/// </summary>
		public void Add(GameObject gameObject)
		{
			SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
			foreach (SkinnedMeshRenderer renderer in skinnedMeshRenderers)
			{
				AddApparel(renderer);
			}
		}

		/// <summary>
		/// Unlists all <see cref="SkinnedMeshRenderer"/>s found in <paramref name="gameObject"/> and its children.
		/// </summary>
		public void Remove(GameObject gameObject)
		{
			SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
			foreach (SkinnedMeshRenderer renderer in skinnedMeshRenderers)
			{
				RemoveApparel(renderer);
			}
		}

		/// <summary>
		/// Adds <paramref name="renderer"/> to the shared rig.
		/// </summary>
		public void AddApparel(SkinnedMeshRenderer renderer)
		{
			if (renderer == null)
			{
				SpaxDebug.Warning("Missing skinned mesh reference.", "", gameObject);
				return;
			}
			if (renderer == reference || body.ContainsValue(renderer))
			{
				SpaxDebug.Warning("Cannot add reference skin or body part as apparel.", "", gameObject);
				return;
			}

			if (!apparel.Contains(renderer))
			{
				apparel.Add(renderer);
				ConfigureApparel(renderer);
			}

			ApplyData(renderer);
		}

		/// <summary>
		/// Removes <paramref name="renderer"/> from the shared rig.
		/// </summary>
		public void RemoveApparel(SkinnedMeshRenderer renderer)
		{
			if (apparel.Contains(renderer))
			{
				apparel.Remove(renderer);

				// Check if a body part has now been left exposed.
				if (coverers.ContainsKey(renderer))
				{
					coverers.Remove(renderer);
					foreach (BodyPart bodyPart in bodyParts)
					{
						bodyPart.Skin.gameObject.SetActive(!coverers.Any(c => c.Value.Locations.Contains(bodyPart.Location)));
					}
				}
			}
		}

		public void ApplyData(SkinnedMeshRenderer renderer)
		{
			renderer.rootBone = reference.rootBone;
			renderer.bones = reference.bones;
		}

		#region Private Methods

		/// <summary>
		/// Get all SkinnedMeshRenderers from this gameobject's children and add them to apparel.
		/// </summary>
		private void AutoCollectApparel()
		{
			SkinnedMeshRenderer[] renderers = transform.GetComponentsInChildren<SkinnedMeshRenderer>();
			foreach (SkinnedMeshRenderer renderer in renderers)
			{
				if (renderer != reference && !body.ContainsValue(renderer) && !apparel.Contains(renderer))
				{
					AddApparel(renderer);
				}
			}
		}

		/// <summary>
		/// Make sure apparel collection matches neither reference nor bodyparts.
		/// </summary>
		private void CleanApparel()
		{
			for (int i = 0; i < apparel.Count; i++)
			{
				if (apparel[i] == reference || body.ContainsValue(apparel[i]))
				{
					RemoveApparel(apparel[i]);
					i--;
				}
			}
		}

		private void ConfigureApparel(SkinnedMeshRenderer renderer)
		{
			// Configure cloth.
			if (renderer.TryGetComponent(out Cloth cloth))
			{
				cloth.capsuleColliders = capsuleColliders;
				cloth.sphereColliders = sphereColliders;
			}

			// Hide skin covered by apparel.
			if (renderer.TryGetComponent(out IEntityApparel entityApparel) && entityApparel.Locations != null && entityApparel.Locations.Count > 0)
			{
				if (!coverers.ContainsKey(renderer))
				{
					coverers.Add(renderer, entityApparel);
				}
				foreach (string location in entityApparel.Locations)
				{
					if (body.ContainsKey(location))
					{
						body[location].gameObject.SetActive(false);
					}
				}
			}
		}

		#endregion Private Methods
	}
}