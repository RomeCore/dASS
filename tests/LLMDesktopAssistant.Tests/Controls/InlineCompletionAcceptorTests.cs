using LLMDesktopAssistant.Controls.Text;

namespace LLMDesktopAssistant.Tests.Controls
{
	/// <summary>
	/// The pure Right-key inline-completion edit: insert one completion character, consume one token character.
	/// </summary>
	public class InlineCompletionAcceptorTests
	{
		[Fact]
		public void Accept_WithNoCompletion_ReturnsNull()
		{
			Assert.Null(InlineCompletionAcceptor.Accept("abc", 3, "", 3));
			Assert.Null(InlineCompletionAcceptor.Accept("abc", 3, null, 3));
		}

		[Fact]
		public void Accept_AtTheTokenEnd_InsertsWithoutConsuming()
		{
			var accepted = InlineCompletionAcceptor.Accept("abc", 3, "123", 3);

			Assert.NotNull(accepted);
			Assert.Equal("abc1", accepted!.Value.Text);
			Assert.Equal(4, accepted.Value.Caret);
		}

		[Fact]
		public void Accept_InsideTheToken_ConsumesOneCharacter()
		{
			// "abc|d e f" → "abc1| e f"
			var accepted = InlineCompletionAcceptor.Accept("abcd e f", 3, "1 2 3", 4);

			Assert.NotNull(accepted);
			Assert.Equal("abc1 e f", accepted!.Value.Text);
			Assert.Equal(4, accepted.Value.Caret);
		}

		[Fact]
		public void Accept_WhenTheCaretIsPastTheToken_DoesNotConsume()
		{
			var accepted = InlineCompletionAcceptor.Accept("abc def", 5, "1", 3);

			Assert.NotNull(accepted);
			Assert.Equal("abc d1ef", accepted!.Value.Text);
			Assert.Equal(6, accepted.Value.Caret);
		}
	}
}
