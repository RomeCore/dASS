using System.Collections.Immutable;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Providers;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The <c>/skill:&lt;name&gt;</c> executor: it injects the substituted body as a non-restorable content part and
	/// keeps the raw model-facing mode.
	/// </summary>
	public class SkillCommandExecutorTests
	{
		private static SlashCommandBoundArguments Arguments(string rest) => new()
		{
			Positionals = [],
			Keyed = ImmutableDictionary<string, ParsedSlashCommandArgument>.Empty,
			RawPositionalArguments = rest,
			RestPositionalArguments = rest
		};

		private static UserMessage Message(string content)
		{
			var message = new UserMessage
			{
				Content = content,
				SenderLogin = "user",
				Visibility = MessageVisibility.Always,
				VisibleTo = [],
				IsVisibleToWhiteList = false
			};

			// Raise the collection events synchronously: the default collection marshals to the Avalonia UI thread,
			// which deadlocks when two test classes do it in parallel without a running dispatcher.
			message.AdditionalData.RaiseInUIThread = false;
			return message;
		}

		private static SlashCommandExecutionContext Context(UserMessage message, string rest) => new()
		{
			Chat = null!,
			Message = message,
			Command = new SlashCommandInfo { Name = "grilling", Namespaces = ["skill"] },
			Token = "skill:grilling",
			RawToken = "grilling",
			RawText = message.Content,
			RawArguments = rest,
			Arguments = Arguments(rest),
			GenerateIntent = true,
			Services = null!
		};

		private static AdditionalMessageContentPart InjectedPart(UserMessage message)
			=> Assert.Single(message.AdditionalData.OfType<AdditionalMessageContentPart>());

		[Fact]
		public async Task Executes_InjectsTheSubstitutedBody_AndKeepsTheRawMode()
		{
			var skill = new SkillInfo { Name = "grilling", Description = "d", Body = "Body: $ARGUMENTS" };
			var message = Message("/skill:grilling do it");
			var ctx = Context(message, "do it");

			var result = await new SkillCommandExecutor(skill).ExecuteAsync(ctx, CancellationToken.None);

			var part = InjectedPart(message);
			Assert.Equal("Body: do it", part.Content);
			Assert.False(part.IsRestorable);
			Assert.Equal("command.skill.used", part.ChipTitle!.Key);

			Assert.True(result.IsSuccess);
			Assert.True(result.Generate);
			Assert.Equal(ModelFacingMode.Raw, result.ModelFacingMode);
		}

		[Fact]
		public async Task Executes_AppendsTheHomeDirectoryNote_WhenTheSkillHasAHome()
		{
			var skill = new SkillInfo
			{
				Name = "grilling",
				Description = "d",
				Body = "SKILL BODY",
				HomeDirectory = @"C:\skills\grilling"
			};
			var message = Message("/skill:grilling");
			var ctx = Context(message, string.Empty);

			await new SkillCommandExecutor(skill).ExecuteAsync(ctx, CancellationToken.None);

			var part = InjectedPart(message);
			Assert.StartsWith("SKILL BODY", part.Content);
			Assert.Contains(@"C:\skills\grilling", part.Content);
		}

		[Fact]
		public async Task Executes_WithoutAHomeDirectory_DoesNotAppendTheNote()
		{
			var skill = new SkillInfo { Name = "grilling", Description = "d", Body = "SKILL BODY" };
			var message = Message("/skill:grilling");
			var ctx = Context(message, string.Empty);

			await new SkillCommandExecutor(skill).ExecuteAsync(ctx, CancellationToken.None);

			Assert.Equal("SKILL BODY", InjectedPart(message).Content);
		}
	}
}
