using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Providers;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The command set collector: the fully-qualified deduplication key, the master gate and per-command overrides.
	/// </summary>
	public class SlashCommandSetCollectorTests
	{
		private sealed class FakeProvider(params SlashCommandInfo[] commands) : ISlashCommandProvider
		{
			public IEnumerable<SlashCommandInfo> GetCommands() => commands;
		}

		private sealed class FakeChatSettingsService(ChatSettings settings) : IChatSettingsService
		{
			public ChatSettings Settings { get; private set; } = settings;

			public event EventHandler? SettingsChanged;

			public void SetSettings(ChatSettings settings)
			{
				Settings = settings;
				SettingsChanged?.Invoke(this, EventArgs.Empty);
			}

			public void LoadFromProfile(string profileName = "default")
			{
			}
		}

		private sealed class FakeAccessor<T>(IEnumerable<T> addons) : IAddonAccessor<T>
		{
			public ReadOnlyObservableCollection<T> Addons { get; } = new(addons.ToList());
		}

		private sealed class FakeServiceProvider(IAddonAccessor<SlashCommandInfo> accessor) : IServiceProvider
		{
			public object? GetService(Type serviceType) =>
				serviceType == typeof(IAddonAccessor<SlashCommandInfo>) ? accessor : null;
		}

		private static SlashCommandInfo Command(string name, params string[] namespaces) => new()
		{
			Name = name,
			Namespaces = [.. namespaces]
		};

		private static SlashCommandSetCollector Collector(ChatSettings settings, params SlashCommandInfo[] commands)
			=> new([new FakeProvider(commands)], new FakeChatSettingsService(settings),
				new FakeServiceProvider(new FakeAccessor<SlashCommandInfo>([])));

		[Fact]
		public void CommandsOfDifferentNamespaces_StayDistinct()
		{
			var collector = Collector(new ChatSettings(),
				Command("grilling", "skill"),
				Command("grilling", "skill", "matt-pocock"));

			var commands = collector.GetAvailableAddons().ToList();

			Assert.Equal(2, commands.Count);
			Assert.Contains(commands, c => c.Key == "skill:grilling");
			Assert.Contains(commands, c => c.Key == "matt-pocock:skill:grilling");
		}

		[Fact]
		public void TrueDuplicates_CollapseIntoOverrides()
		{
			var collector = Collector(new ChatSettings(),
				Command("grilling", "skill"),
				Command("grilling", "skill"));

			var command = Assert.Single(collector.GetAvailableAddons());

			Assert.Single(command.Overrides);
		}

		[Fact]
		public void MasterGate_WhenDisabled_ReturnsNoCommands()
		{
			var settings = new ChatSettings();
			settings.Commands.EnableCommands = false;

			var collector = Collector(settings, Command("grilling", "skill"));

			Assert.Empty(collector.GetAddonsForChat());
		}

		[Fact]
		public void MasterGate_WhenEnabled_ReturnsCommands()
		{
			var collector = Collector(new ChatSettings(), Command("grilling", "skill"));

			var command = Assert.Single(collector.GetAddonsForChat());

			Assert.Equal("grilling", command.Name);
		}

		[Fact]
		public void Change_DisablesTheCommand()
		{
			var settings = new ChatSettings();
			settings.Commands.CommandsSet.Changes.Add("skill:grilling", new SlashCommandChange { Enabled = false });

			var collector = Collector(settings, Command("grilling", "skill"));

			Assert.Empty(collector.GetAddonsForChat());
		}

		[Fact]
		public void Change_DoesNotAffectASameNamedCommandOfAnotherNamespace()
		{
			var settings = new ChatSettings();
			settings.Commands.CommandsSet.Changes.Add("skill:grilling", new SlashCommandChange { Enabled = false });

			var collector = Collector(settings,
				Command("grilling", "skill"),
				Command("grilling", "agent"));

			var remaining = Assert.Single(collector.GetAddonsForChat());

			Assert.Equal("agent:grilling", remaining.Key);
			Assert.Equal(new[] { "agent" }, remaining.Namespaces);
		}
	}
}
