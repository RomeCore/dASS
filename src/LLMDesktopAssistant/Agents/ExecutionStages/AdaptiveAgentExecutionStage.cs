using System.Text;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Utils.Json;
using RCLargeLanguageModels;
using Serilog;

namespace LLMDesktopAssistant.Agents.ExecutionStages
{
	[JsonDerived(typeof(AgentExecutionStage), "adaptive")]
	public class AdaptiveAgentExecutionStage : MentionableBaseAgentExecutionStage
	{
		private readonly Random _random = new();

		private int _maxVisibleRounds = 1;
		/// <summary>
		/// Maximum number of rounds that is visible to router agent.
		/// </summary>
		public int MaxVisibleRounds
		{
			get => _maxVisibleRounds;
			set => SetProperty(ref _maxVisibleRounds, value);
		}

		private string? _additionalRouterPrompt = null;
		/// <summary>
		/// Additional prompt to be added to system prompt for router agent.
		/// </summary>
		public string? AdditionalRouterPrompt
		{
			get => _additionalRouterPrompt;
			set => SetProperty(ref _additionalRouterPrompt, value);
		}

		private bool _treatUserLikeAgentsAsUsers = true;
		/// <summary>
		/// When enabled, messages of agents that identify themselves as users
		/// (<see cref="AgentInformation.IdentifyAsUser"/>) are treated as user messages
		/// when building the router context.
		/// </summary>
		public bool TreatUserLikeAgentsAsUsers
		{
			get => _treatUserLikeAgentsAsUsers;
			set => SetProperty(ref _treatUserLikeAgentsAsUsers, value);
		}

		private bool _enforceRouterSelection = false;
		/// <summary>
		/// Whether to enforce router selection for the next agent. If disabled, router can choose none.
		/// If enabled and router not chooses anything, a random weighted agent will be selected.
		/// </summary>
		public bool EnforceRouterSelection
		{
			get => _enforceRouterSelection;
			set => SetProperty(ref _enforceRouterSelection, value);
		}

		protected override async Task<Guid?> SelectNextAgentAsync(List<ChatAgentInstance> selectFrom,
			AgentPreExecutionContext context, CancellationToken cancellationToken = default)
		{
			if (await base.SelectNextAgentAsync(selectFrom, context, cancellationToken) is Guid nextAgent)
				return nextAgent;

			var routerModelName = context.Chat.Services.GetRequiredService<IChatSettingsService>().Settings.Models.GetEffectiveSelection().AgenticRouterModel;
			if (string.IsNullOrEmpty(routerModelName))
				throw new InvalidOperationException("Agentic router model is not selected. Please select a router model in chat settings.");

			var modelManager = context.Services.GetRequiredService<IModelManager>();
			LLModel llm;
			try
			{
				llm = modelManager.GetModel(routerModelName);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Agentic router model '{routerModelName}' is not available: {ex.Message}");
			}

			var templates = context.Services.GetRequiredService<ITemplateLibraryAccessor>();
			var agentTaskExecutor = context.Services.GetRequiredService<IAgentTaskExecutor>();

			var agents = selectFrom
				.Select(a => context.AgentManager.GetAgentDescriptor(a.AgentId))
				.ToList();

			if (agents.Count == 0)
				return null;

			var agentNames = string.Join("\n", agents.Select(a => a.Info.Name));

			var template = templates.GetMessagesTemplate("router_prompt");
			var messages = template.RenderToAgent(new
			{
				agents = agents.Select(a => new
				{
					name = a.Info.Name,
					description = a.Info.Description
				}).ToArray(),
				enforce_selection = EnforceRouterSelection,
				additional_prompt = AdditionalRouterPrompt,
				context = SelectContext(context),
			});

			var task = await agentTaskExecutor.Execute(new AgentTaskLaunchParameters
			{
				TaskName = LocalizationManager.LocalizeStatic("agent.selection.task"),
				InitialMessages = [..messages],
				Model = llm,
			}, cancellationToken);

			var content = task.LastGeneratedContent?.TrimStart('@');
			var selectedAgent = agents.FirstOrDefault(a => a.Info.Name == content)?.Id;

			Log.Information("Router model selected next agent: {Content}", content);

			if (selectedAgent == null && EnforceRouterSelection)
			{
				Log.Information("Picking a random agent...");

				// Pick a random agent, if router did not choose anything
				double weightSum = selectFrom.Sum(a => a.Weight);
				double randomValue = _random.NextDouble() * weightSum;
				double currentWeight = 0;

				foreach (var agent in selectFrom)
				{
					currentWeight += agent.Weight;
					if (currentWeight >= randomValue)
						return agent.AgentId;
				}
			}

			return selectedAgent;
		}

		private string? SelectContext(AgentPreExecutionContext context)
		{
			var rounds = MessagesInterface.GroupMessagesIntoRounds(context.Chat.Messages, MaxVisibleRounds, TreatUserLikeAgentsAsUsers);
			var quoteRenderer = context.Services.GetRequiredService<IChatMessageQuoteRenderer>();

			var sb = new StringBuilder();

			foreach (var round in rounds)
			{
				foreach (var message in round)
				{
					sb.AppendLine(quoteRenderer.Render(message.Message).Content);
				}
			}

			return sb.ToString();
		}
	}
}
