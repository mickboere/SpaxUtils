using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Local multiplayer screen division settings, applied to the <see cref="SplitScreenService"/>.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(SplitScreenSettings), menuName = "ScriptableObjects/Camera/" + nameof(SplitScreenSettings))]
	public class SplitScreenSettings : ScriptableObject, IService, ISettingsSupplier
	{
		[Setting("multiplayer.splitAxis", "Multiplayer/Local", "Split Axis")]
		[SerializeField, Tooltip("Horizontal stacks players top to bottom, Vertical places them side by side.")]
		private EnumSetting<SplitLayout> splitLayout = new EnumSetting<SplitLayout>(SplitLayout.Horizontal);

		private SplitScreenService splitScreenService;

		public void InjectDependencies(SplitScreenService splitScreenService)
		{
			this.splitScreenService = splitScreenService;
			splitLayout.ChangedEvent += OnSplitLayoutChanged;
			splitScreenService.SetLayout(splitLayout.Value);
		}

		private void OnSplitLayoutChanged(Setting setting, int player)
		{
			splitScreenService.SetLayout(splitLayout.Value);
		}
	}
}
