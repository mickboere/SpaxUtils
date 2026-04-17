using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.UI
{
	public class PauseMenuScreen : UIScreen
	{
		[SerializeField] private OptionsMenuUI optionMenu;
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers))] string[] options;

		private IAgent agent;
		private ICommunicationChannel comms;
		private RuntimeDataService runtimeDataService;
		private UIScreenManager screenManager;
		private GameService gameService;
		private DialogueBoxService dialogueBoxService;

		public void InjectDependencies(IAgent agent, ICommunicationChannel comms, RuntimeDataService runtimeDataService,
			UIScreenManager screenManager, GameService gameService, DialogueBoxService dialogueBoxService)
		{
			this.screenManager = screenManager;
			this.agent = agent;
			this.comms = comms;
			this.runtimeDataService = runtimeDataService;
			this.gameService = gameService;
			this.dialogueBoxService = dialogueBoxService;
		}

		protected void Awake()
		{
			comms.Listen<IRequestOptionsMsg>(this, OnRequestMenuOptionsMsg);
			optionMenu.Initialize(Context, "");
		}

		protected override void OnDestroy()
		{
			comms.StopListening(this);
			base.OnDestroy();
		}

		private void OnRequestMenuOptionsMsg(IRequestOptionsMsg msg)
		{
			// If the OptionsMenu requesting options is the pause menu, provide it with all configured menus.
			if (msg.Context == ContextIdentifiers.PAUSE)
			{
				// Add Resume option.
				msg.AddOption(new Option("Resume", "", (option) => screenManager.SwitchContext(null)));

				// Add saving option.
				msg.AddOption(new Option("Save", "", (option) =>
				{
					dialogueBoxService.ShowConfirmCancel("Save Data", null, "All existing data will be overwritten.\n\nContinue?",
						() =>
						{
							agent.SaveData();
							runtimeDataService.SaveProfileToDisk();
							screenManager.SwitchContext(null);
						});
				}));

				// Add all other options.
				foreach (string option in options)
				{
					msg.AddOption(new Option(option, "", (_) => screenManager.SwitchContext(option)));
				}

				// Add Main Menu option.
				msg.AddOption(new Option("Main Menu", "", (option) =>
				{
					dialogueBoxService.ShowConfirmCancel("Return to Main Menu", null, "Warning; all unsaved progress will be lost.\n\nContinue?",
						() => { gameService.SwitchState(GameStateIdentifiers.MAIN_MENU, reason: GameStateSwitchReason.UserRequest); });
				}));

				// Add Quit option.
				msg.AddOption(new Option("Quit", "", (option) =>
				{
					dialogueBoxService.ShowConfirmCancel("Quit Game", null, "Warning; all unsaved progress will be lost.\n\nAre you sure you want to quit?",
						() =>
						{
#if UNITY_EDITOR
							UnityEditor.EditorApplication.isPlaying = false;
#else
					Application.Quit();
#endif
						});

				}));
			}
		}
	}
}
