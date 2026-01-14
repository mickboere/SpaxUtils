using System;
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
	[DefaultExecutionOrder(50)]
	public class EntityAppearanceHandler : EntityComponentMono
	{
		[Serializable]
		public class BodyPart
		{
			public string Location => location;
			public SkinnedMeshRenderer Skin => skinOverride ?? skin;

			[SerializeField, ConstDropdown(typeof(IBodyLocations))] private string location;
			[SerializeField] private SkinnedMeshRenderer skin;

			private SkinnedMeshRenderer skinOverride;

			public void Override(SkinnedMeshRenderer skinOverride)
			{
				skin.gameObject.SetActive(false);
				this.skinOverride = skinOverride;
			}
		}

		public event Action UpdatedActiveRenderersEvent;

		public IReadOnlyList<BodyPart> BodyParts => bodyParts;
		public IReadOnlyDictionary<string, SkinnedMeshRenderer> Body => body;
		public IReadOnlyList<SkinnedMeshRenderer> Apparel => apparel;

		// Skinned-only renderers (body + skinned apparel), used for rig/skeleton related logic.
		public List<SkinnedMeshRenderer> ActiveRenderers { get; private set; } = new List<SkinnedMeshRenderer>();

		// All visuals that should receive appearance effects (MPBs): skinned + non-skinned equipment visuals.
		public IReadOnlyList<Renderer> ActiveVisualRenderers => activeVisualRenderers;

		[SerializeField] private Transform skeletonRoot;
		[SerializeField] private SkinnedMeshRenderer reference;
		[SerializeField] private List<BodyPart> bodyParts;
		[SerializeField] private List<SkinnedMeshRenderer> apparel = new List<SkinnedMeshRenderer>();
		[SerializeField] private bool autoCollectApparel;

		// Non-skinned visuals (weapons, shields, etc.) that should receive appearance effects (MPBs).
		[SerializeField] private List<Renderer> extraVisuals = new List<Renderer>();

		private Dictionary<string, SkinnedMeshRenderer> body;
		private Dictionary<SkinnedMeshRenderer, IEntityApparel> coverers = new Dictionary<SkinnedMeshRenderer, IEntityApparel>();
		private CapsuleCollider[] capsuleColliders;
		private ClothSphereColliderPair[] sphereColliders;

		private List<Renderer> activeVisualRenderers = new List<Renderer>();

		private HashSet<SkinnedMeshRenderer> tempSkinnedSet = new HashSet<SkinnedMeshRenderer>();
		private HashSet<Renderer> tempVisualSet = new HashSet<Renderer>();
		private HashSet<Renderer> activeVisualSet = new HashSet<Renderer>();

		private int refreshLock;
		private bool refreshQueued;

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
			if (bodyParts == null)
			{
				// Nothing to initialize yet, component was probably just added.
				return;
			}

			// Collect bodyparts to map out base body skin locations.
			body = bodyParts.ToDictionary((k) => k.Location, (v) => v.Skin);

			// Gather skeleton colliders for cloth renderers.
			capsuleColliders = skeletonRoot.GetComponentsInChildren<CapsuleCollider>();
			sphereColliders = skeletonRoot.GetComponentsInChildren<SphereCollider>().Select((c) => new ClothSphereColliderPair(c)).ToArray();

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
					ApplySkeleton(bodyPart.Skin);
				}
			}

			foreach (SkinnedMeshRenderer sharer in apparel)
			{
				if (sharer != null)
				{
					ConfigureApparel(sharer);
					ApplySkeleton(sharer);
				}
			}

			RequestRefresh();
			FlushRefresh();
		}

		/// <summary>
		/// Adds all <see cref="SkinnedMeshRenderer"/>s found in <paramref name="gameObject"/> and its children to the shared rig.
		/// Also tracks non-skinned <see cref="Renderer"/>s as visuals (weapons/shields) for appearance effects.
		/// </summary>
		public void Add(GameObject gameObject)
		{
			if (gameObject == null)
			{
				return;
			}

			BeginBatch();

			SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			foreach (SkinnedMeshRenderer renderer in skinnedMeshRenderers)
			{
				AddApparel(renderer);
			}

			Renderer[] visuals = gameObject.GetComponentsInChildren<Renderer>(true);
			foreach (Renderer renderer in visuals)
			{
				if (renderer == null)
				{
					continue;
				}

				if (renderer is SkinnedMeshRenderer)
				{
					continue;
				}

				AddExtraVisual(renderer);
			}

			EndBatch();
		}

		/// <summary>
		/// Unlists all <see cref="SkinnedMeshRenderer"/>s found in <paramref name="gameObject"/> and its children.
		/// Also removes tracked non-skinned <see cref="Renderer"/> visuals.
		/// </summary>
		public void Remove(GameObject gameObject)
		{
			if (gameObject == null)
			{
				return;
			}

			BeginBatch();

			SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			foreach (SkinnedMeshRenderer renderer in skinnedMeshRenderers)
			{
				RemoveApparel(renderer);
			}

			Renderer[] visuals = gameObject.GetComponentsInChildren<Renderer>(true);
			foreach (Renderer renderer in visuals)
			{
				if (renderer == null)
				{
					continue;
				}

				if (renderer is SkinnedMeshRenderer)
				{
					continue;
				}

				RemoveExtraVisual(renderer);
			}

			EndBatch();
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

			ApplySkeleton(renderer);
			RequestRefresh();
		}

		/// <summary>
		/// Removes <paramref name="renderer"/> from the shared rig.
		/// </summary>
		public void RemoveApparel(SkinnedMeshRenderer renderer)
		{
			if (!apparel.Contains(renderer))
			{
				return;
			}

			apparel.Remove(renderer);

			// Check if a body part has now been left exposed.
			if (coverers.ContainsKey(renderer))
			{
				coverers.Remove(renderer);
				foreach (BodyPart bodyPart in bodyParts)
				{
					bodyPart.Skin.gameObject.SetActive(!coverers.Any((c) => c.Value.Locations.Contains(bodyPart.Location)));
				}
			}

			RequestRefresh();
		}

		/// <summary>
		/// Adds a non-skinned visual renderer (weapon/shield/etc.) so it receives appearance effects (MPBs).
		/// </summary>
		public void AddExtraVisual(Renderer renderer)
		{
			if (renderer == null)
			{
				return;
			}

			if (!extraVisuals.Contains(renderer))
			{
				extraVisuals.Add(renderer);
				RequestRefresh();
			}
		}

		/// <summary>
		/// Removes a non-skinned visual renderer (weapon/shield/etc.) from appearance effects (MPBs).
		/// </summary>
		public void RemoveExtraVisual(Renderer renderer)
		{
			if (renderer == null)
			{
				return;
			}

			if (extraVisuals.Contains(renderer))
			{
				extraVisuals.Remove(renderer);
				RequestRefresh();
			}
		}

		/// <summary>
		/// Will make <paramref name="renderer"/> conform to the entity's skeleton.
		/// </summary>
		public void ApplySkeleton(SkinnedMeshRenderer renderer)
		{
			renderer.rootBone = reference.rootBone;
			renderer.bones = reference.bones;
		}

		#region Private Methods

		private void BeginBatch()
		{
			refreshLock++;
		}

		private void EndBatch()
		{
			refreshLock--;
			if (refreshLock < 0)
			{
				refreshLock = 0;
			}

			FlushRefresh();
		}

		private void RequestRefresh()
		{
			refreshQueued = true;
			FlushRefresh();
		}

		private void FlushRefresh()
		{
			if (!Application.isPlaying)
			{
				return;
			}

			if (!refreshQueued)
			{
				return;
			}

			if (refreshLock > 0)
			{
				return;
			}

			refreshQueued = false;
			RebuildActiveLists();
		}

		private void RebuildActiveLists()
		{
			tempSkinnedSet.Clear();
			tempVisualSet.Clear();

			foreach (SkinnedMeshRenderer renderer in body.Values)
			{
				if (renderer != null)
				{
					tempSkinnedSet.Add(renderer);
					tempVisualSet.Add(renderer);
				}
			}

			for (int i = 0; i < apparel.Count; i++)
			{
				SkinnedMeshRenderer renderer = apparel[i];
				if (renderer == null)
				{
					apparel.RemoveAt(i);
					i--;
					continue;
				}

				tempSkinnedSet.Add(renderer);
				tempVisualSet.Add(renderer);
			}

			for (int i = 0; i < extraVisuals.Count; i++)
			{
				Renderer renderer = extraVisuals[i];
				if (renderer == null)
				{
					extraVisuals.RemoveAt(i);
					i--;
					continue;
				}

				tempVisualSet.Add(renderer);
			}

			bool changed = !tempVisualSet.SetEquals(activeVisualSet);
			if (!changed)
			{
				return;
			}

			activeVisualSet.Clear();
			foreach (Renderer renderer in tempVisualSet)
			{
				activeVisualSet.Add(renderer);
			}

			ActiveRenderers.Clear();
			foreach (SkinnedMeshRenderer renderer in tempSkinnedSet)
			{
				ActiveRenderers.Add(renderer);
			}

			activeVisualRenderers.Clear();
			foreach (Renderer renderer in activeVisualSet)
			{
				activeVisualRenderers.Add(renderer);
			}

			UpdatedActiveRenderersEvent?.Invoke();
		}

		/// <summary>
		/// Get all SkinnedMeshRenderers from this gameobject's children and add them to apparel.
		/// </summary>
		private void AutoCollectApparel()
		{
			// Remove null references.
			for (int i = 0; i < apparel.Count; i++)
			{
				if (apparel[i] == null)
				{
					apparel.RemoveAt(i);
					i--;
				}
			}

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

			RequestRefresh();
		}

		#endregion Private Methods
	}
}
