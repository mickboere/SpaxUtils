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

		public void InjectDependencies(IAgent agent, ICommunicationChannel comms, RuntimeDataService runtimeDataService, UIScreenManager screenManager)
		{
			this.screenManager = screenManager;
			this.agent = agent;
			this.comms = comms;
			this.runtimeDataService = runtimeDataService;
		}

		protected void Awake()
		{
			comms.Listen<IRequestOptionsMsg>(this, OnRequestMenuOptionsMsg);
			optionMenu.Initialize(Context);
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
					agent.SaveData();
					runtimeDataService.SaveProfileToDisk();
					screenManager.SwitchContext(null);
				}));

				// Add all other options.
				foreach (string option in options)
				{
					msg.AddOption(new Option(option, "", (_) => screenManager.SwitchContext(option)));
				}

				// Add Quit option.
				msg.AddOption(new Option("Quit", "", (option) =>
				{
					SpaxDebug.Log("Quit game");
					Application.Quit();
				}));
			}
		}
	}
}
