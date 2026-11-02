using LLMDesktopAssistant.Cli;
using Serilog.Events;

namespace LLMDesktopAssistant.Tests.Cli
{
	public class CommandLineExecutorTests
	{
		[Fact]
		public void TryParse_WithNoArguments_StartsTheGuiWithDefaults()
		{
			var success = CommandLineExecutor.TryParse([], out var invocation, out var error);
			var defaults = new CommandLineParameters();

			Assert.True(success);
			Assert.Null(error);
			Assert.Null(invocation.Action);
			Assert.Empty(invocation.Arguments);
			Assert.Equal(defaults.NoSplash, invocation.Parameters.NoSplash);
			Assert.Equal(defaults.LogLevel, invocation.Parameters.LogLevel);
		}

		[Fact]
		public void TryParse_WithNoSplashFlag_DisablesTheSplash()
		{
			var success = CommandLineExecutor.TryParse(["--no-splash"], out var invocation, out var error);

			Assert.True(success);
			Assert.Null(error);
			Assert.True(invocation.Parameters.NoSplash);
		}

		[Theory]
		[InlineData("trace", LogEventLevel.Verbose)]
		[InlineData("debug", LogEventLevel.Debug)]
		[InlineData("info", LogEventLevel.Information)]
		[InlineData("warn", LogEventLevel.Warning)]
		[InlineData("error", LogEventLevel.Error)]
		[InlineData("fatal", LogEventLevel.Fatal)]
		public void TryParse_WithLogLevel_AcceptsEveryDocumentedLevel(string value, LogEventLevel expected)
		{
			var success = CommandLineExecutor.TryParse(["--log-level", value], out var invocation, out _);

			Assert.True(success);
			Assert.Equal(expected, invocation.Parameters.LogLevel);
		}

		[Fact]
		public void TryParse_WithInlineLogLevelValue_AcceptsTheEqualsForm()
		{
			var success = CommandLineExecutor.TryParse(["--log-level=debug"], out var invocation, out _);

			Assert.True(success);
			Assert.Equal(LogEventLevel.Debug, invocation.Parameters.LogLevel);
		}

		[Fact]
		public void TryParse_WithUnknownLogLevel_Fails()
		{
			var success = CommandLineExecutor.TryParse(["--log-level", "loud"], out _, out var error);

			Assert.False(success);
			Assert.Contains("Unknown log level", error);
		}

		[Fact]
		public void TryParse_WithMissingLogLevelValue_Fails()
		{
			var success = CommandLineExecutor.TryParse(["--log-level"], out _, out var error);

			Assert.False(success);
			Assert.Contains("requires a value", error);
		}

		[Fact]
		public void TryParse_WithValueForNoSplash_Fails()
		{
			var success = CommandLineExecutor.TryParse(["--no-splash=true"], out _, out var error);

			Assert.False(success);
			Assert.Contains("does not take a value", error);
		}

		[Fact]
		public void TryParse_WithUnknownOption_Fails()
		{
			var success = CommandLineExecutor.TryParse(["--nope"], out _, out var error);

			Assert.False(success);
			Assert.Contains("Unknown option", error);
		}

		[Fact]
		public void TryParse_WithPositionalToken_ReportsUnknownAction()
		{
			// No headless action is declared yet, and a typo must never start the GUI silently.
			var success = CommandLineExecutor.TryParse(["run"], out _, out var error);

			Assert.False(success);
			Assert.Contains("Unknown action", error);
		}

		[Theory]
		[InlineData("-h")]
		[InlineData("--help")]
		[InlineData("-?")]
		public void TryParse_WithHelpFlag_RequestsUsage(string flag)
		{
			var success = CommandLineExecutor.TryParse([flag], out var invocation, out _);

			Assert.True(success);
			Assert.True(invocation.ShowHelp);
		}

		[Theory]
		[InlineData("-v")]
		[InlineData("--version")]
		public void TryParse_WithVersionFlag_RequestsVersion(string flag)
		{
			var success = CommandLineExecutor.TryParse([flag], out var invocation, out _);

			Assert.True(success);
			Assert.True(invocation.ShowVersion);
		}
	}
}
