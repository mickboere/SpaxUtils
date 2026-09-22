using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaxUtils
{
	/// <summary>
	/// Per-player input settings: camera look response and binding overrides.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(PlayerInputSettings), menuName = "ScriptableObjects/Input/" + nameof(PlayerInputSettings))]
	public class PlayerInputSettings : ScriptableObject, IService, ISettingsSupplier
	{
		private const string CAMERA = "Input/Camera";
		private const string CONTROLS = "Input/Controls";

		/// <summary>
		/// Every player's binding overrides, as JSON.
		/// </summary>
		public BindingsSetting Bindings => bindings;

		[Setting("input.sensitivityX", CAMERA, "Sensitivity X", 0.1f, 3f, Scope = SettingScope.Player, Format = "0.00x")]
		[SerializeField] private FloatSetting sensitivityX = new FloatSetting(1f);
		[Setting("input.sensitivityY", CAMERA, "Sensitivity Y", 0.1f, 3f, Scope = SettingScope.Player, Format = "0.00x")]
		[SerializeField] private FloatSetting sensitivityY = new FloatSetting(1f);
		[Setting("input.invertX", CAMERA, "Invert X", Scope = SettingScope.Player)]
		[SerializeField] private BoolSetting invertX = new BoolSetting(false);
		[Setting("input.invertY", CAMERA, "Invert Y", Scope = SettingScope.Player)]
		[SerializeField] private BoolSetting invertY = new BoolSetting(false);
		[Setting("input.bindings", CONTROLS, "Controls", Scope = SettingScope.Player)]
		[SerializeField] private BindingsSetting bindings = new BindingsSetting();

		private PlayerInputService playerInputService;

		public void InjectDependencies(PlayerInputService playerInputService)
		{
			this.playerInputService = playerInputService;
			playerInputService.WrapperRegisteredEvent += OnWrapperRegistered;
			bindings.ChangedEvent += OnBindingsChanged;

			foreach (var kvp in playerInputService.Wrappers)
			{
				ApplyBindings(kvp.Key, kvp.Value);
			}
		}

		protected void OnDestroy()
		{
			if (playerInputService != null)
			{
				playerInputService.WrapperRegisteredEvent -= OnWrapperRegistered;
			}
		}

		/// <summary>
		/// Multiplier on <paramref name="player"/>'s look input, with inversion as a negative axis.
		/// </summary>
		public Vector2 GetLookScale(int player)
		{
			return new Vector2(
				sensitivityX.Get(player) * (invertX.Get(player) ? -1f : 1f),
				sensitivityY.Get(player) * (invertY.Get(player) ? -1f : 1f));
		}

		/// <summary>
		/// Stores <paramref name="player"/>'s current binding overrides.
		/// </summary>
		public void SaveBindings(int player, InputActionAsset actions)
		{
			bindings.Set(actions.SaveBindingOverridesAsJson(), player);
		}

		private void OnWrapperRegistered(int player, PlayerInputWrapper wrapper)
		{
			ApplyBindings(player, wrapper);
		}

		private void OnBindingsChanged(Setting setting, int player)
		{
			if (playerInputService.TryGet(player, out PlayerInputWrapper wrapper))
			{
				ApplyBindings(player, wrapper);
			}
		}

		private void ApplyBindings(int player, PlayerInputWrapper wrapper)
		{
			InputActionAsset actions = wrapper != null && wrapper.PlayerInput != null ? wrapper.PlayerInput.actions : null;
			if (actions == null)
			{
				return;
			}

			// Clear first: P0 runs on the shared asset, whose overrides survive play sessions and leak into clones.
			actions.RemoveAllBindingOverrides();
			string json = bindings.Get(player);
			if (!string.IsNullOrEmpty(json))
			{
				actions.LoadBindingOverridesFromJson(json);
			}
		}
	}
}
