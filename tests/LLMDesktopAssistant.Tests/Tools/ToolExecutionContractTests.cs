using System.Text.Json.Nodes;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.Tests.Tools
{
	/// <summary>
	/// The two argument channels and the projection rule of the neutral call:
	/// <c>ArgumentsOverride ?? parsed(call.Arguments)</c> must always describe the set that will
	/// actually run, and the task call must keep only native attachments out of the added data.
	/// </summary>
	public class ToolExecutionContractTests
	{
		[Fact]
		public void AnOverride_WinsOverTheRawArguments()
		{
			var call = new FakeToolCall { Arguments = """{"path":"original.txt"}""" };
			JsonNode? overridden = JsonNode.Parse("""{"path":"override.txt"}""");
			var accessor = new ToolArgumentsAccessor(call, () => overridden);

			Assert.Same(overridden, accessor.Effective);
		}

		[Fact]
		public void NoOverride_ParsesTheRawArguments()
		{
			var call = new FakeToolCall { Arguments = """{"path":"a.txt"}""" };
			var accessor = new ToolArgumentsAccessor(call, () => null);

			Assert.Equal("a.txt", accessor.Effective?["path"]?.GetValue<string>());
		}

		[Fact]
		public void ChangedRawArguments_RecomputeTheEffectiveSet()
		{
			var call = new FakeToolCall { Arguments = """{"path":"a.txt"}""" };
			var accessor = new ToolArgumentsAccessor(call, () => null);

			Assert.Equal("a.txt", accessor.Effective?["path"]?.GetValue<string>());

			call.Arguments = """{"path":"b.txt"}""";

			Assert.Equal("b.txt", accessor.Effective?["path"]?.GetValue<string>());
		}

		[Fact]
		public void ChangedOverrideReference_RecomputeTheEffectiveSet()
		{
			var call = new FakeToolCall { Arguments = "{}" };
			JsonNode? overridden = JsonNode.Parse("""{"value":1}""");
			var accessor = new ToolArgumentsAccessor(call, () => overridden);

			Assert.Equal(1, accessor.Effective?["value"]?.GetValue<int>());

			overridden = JsonNode.Parse("""{"value":2}""");

			Assert.Equal(2, accessor.Effective?["value"]?.GetValue<int>());
		}

		[Fact]
		public void ClearedOverride_FallsBackToTheRawArguments()
		{
			var call = new FakeToolCall { Arguments = """{"path":"raw.txt"}""" };
			JsonNode? overridden = JsonNode.Parse("""{"path":"override.txt"}""");
			var accessor = new ToolArgumentsAccessor(call, () => overridden);

			Assert.Equal("override.txt", accessor.Effective?["path"]?.GetValue<string>());

			overridden = null;

			Assert.Equal("raw.txt", accessor.Effective?["path"]?.GetValue<string>());
		}

		[Fact]
		public void EmptyArguments_ProduceNull()
		{
			var call = new FakeToolCall { Arguments = string.Empty };
			var accessor = new ToolArgumentsAccessor(call, () => null);

			Assert.Null(accessor.Effective);
		}

		[Fact]
		public void ANativeAttachmentPart_IsProjectedIntoAttachments()
		{
			var call = new AgentToolCall { ToolCallId = "1", ToolName = "tool" };
			var part = new NativeAttachmentMessagePart { NativeAttachment = new ImageAttachment("data:image/png;base64,AAA") };

			call.AddAttachment(part);

			var attachment = Assert.IsType<AgentNativeAttachment>(Assert.Single(call.Attachments));
			Assert.Equal(AttachmentKind.Image, attachment.Type);
			Assert.Equal("data:image/png;base64,AAA", attachment.Url);
		}

		[Fact]
		public void ANonAttachmentPart_IsDropped()
		{
			var call = new AgentToolCall { ToolCallId = "1", ToolName = "tool" };

			call.AddAttachment(new AdditionalMessagePart { ChipTitle = null });

			Assert.Empty(call.Attachments);
		}

		/// <summary>
		/// A minimal neutral call for accessor tests. Only the members the accessor reads are stateful.
		/// </summary>
		private sealed class FakeToolCall : IToolCall
		{
			public string ToolName => "fake";

			public string Arguments { get; set; } = "{}";

			public ToolStatus Status { get; set; }

			public ToolBehaviour? ExpectedBehaviour { get; set; }

			public string? ResultContent { get; set; }

			public bool UseMarkdown { get; set; }

			public JsonNode? StructuredResult { get; set; }

			public VisualIconKind? StatusIcon { get; set; }

			public string? StatusTitle { get; set; }

			public CancellationTokenSource? SynchronizationWait { get; set; }

			public CancellationTokenSource? CancellationTokenSource { get; set; }

			public void AddAttachment(IChatAttachment attachment) { }

			public bool RemoveAttachment(IChatAttachment attachment) { return false; }

			public void ClearAttachments() { }

			public IEnumerable<IChatAttachment> GetAttachments() => [];

			public void SetProgress(double? value, double min, double max)
			{
			}
		}
	}
}
