using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(EntityResourceLibrary), menuName = "ScriptableObjects/" + nameof(EntityResourceLibrary))]
	public class EntityResourceLibrary : ScriptableObject, IService
	{
		[Serializable]
		public class MaterialList
		{
			public Material source;
			public List<Material> replacements;
		}

		public IReadOnlyDictionary<string, MaterialList> Materials
		{
			get
			{
				if (_materials == null)
				{
					_materials = materials.ToDictionary((m) => m.source.name, (m) => m);
				}
				return _materials;
			}
		}
		private Dictionary<string, MaterialList> _materials;

		[SerializeField] private List<MaterialList> materials;
	}
}
