using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The single attachment write path: the chat call stores UI parts as is and wraps native/content
	/// payloads into invisible parts; the task call projects them onto its own two concrete types.
	/// </summary>
	public class ChatAttachmentProjectionTests
	{
		[Fact]
		public void AChatUiPart_IsStoredAsIs()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			var part = new AdditionalMessagePart { ChipTitle = null };

			session.Call.AddAttachment(part);

			Assert.Same(part, Assert.Single(session.Call.AdditionalData));
			Assert.True(part.IsVisible);
		}

		[Fact]
		public void ANativeAttachment_BecomesAnInvisibleNativePart()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			var native = new ImageAttachment("data:image/png;base64,AAA");

			session.Call.AddAttachment(new ChatNativeAttachment(native));

			var part = Assert.Single(session.Call.AdditionalData.OfType<NativeAttachmentMessagePart>());
			Assert.False(part.IsVisible);
			Assert.Equal(native, part.NativeAttachment);
			Assert.Equal(native, Assert.Single(session.Call.GetNativeAttachments()));
		}

		[Fact]
		public void ANativeAttachment_FromAKindAndUrl_BuildsTheNativeForm()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());

			session.Call.AddAttachment(new ChatNativeAttachment(AttachmentKind.Audio, "data:audio/wav;base64,AAA"));

			var native = Assert.Single(session.Call.GetNativeAttachments());
			Assert.IsType<AudioAttachment>(native);
		}

		[Fact]
		public void AContentAttachment_BecomesAnInvisiblePartAndReachesThePrompt()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Call.ResultContent = "result";

			session.Call.AddAttachment(new ChatContentAttachment("the notes"));

			var part = Assert.Single(session.Call.AdditionalData.OfType<AdditionalMessageContentPart>());
			Assert.False(part.IsVisible);
			Assert.Equal("the notes", part.Content);
			Assert.Equal("result\n\nthe notes", ToolCallContentProjector.Project(session.Call));
			Assert.Empty(session.Call.GetNativeAttachments());
		}

		[Fact]
		public void AnUnknownAttachment_Throws()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());

			Assert.Throws<InvalidOperationException>(() => session.Call.AddAttachment(new UnknownAttachment()));
		}

		[Fact]
		public void TheTaskCall_ProjectsNativeAndContentAndDropsChatUi()
		{
			var call = new AgentToolCall { ToolCallId = "1", ToolName = "tool" };

			call.AddAttachment(new ChatNativeAttachment(new ImageAttachment("data:image/png;base64,AAA")));
			call.AddAttachment(new ChatContentAttachment("the notes"));
			call.AddAttachment(new AdditionalMessagePart { ChipTitle = null });

			Assert.Equal(2, call.Attachments.Count);
			var native = Assert.IsType<AgentNativeAttachment>(call.Attachments[0]);
			Assert.Equal(AttachmentKind.Image, native.Type);
			var content = Assert.IsType<AgentContentAttachment>(call.Attachments[1]);
			Assert.Equal("the notes", content.Content);
		}

		[Fact]
		public void AnUnknownTaskAttachment_Throws()
		{
			var call = new AgentToolCall { ToolCallId = "1", ToolName = "tool" };

			Assert.Throws<InvalidOperationException>(() => call.AddAttachment(new UnknownAttachment()));
		}

		private sealed class UnknownAttachment : IChatAttachment
		{
		}
	}
}
