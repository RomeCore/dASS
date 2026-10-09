using System.Linq;
using LLMDesktopAssistant.InputCompletion;

namespace LLMDesktopAssistant.Tests.InputCompletion
{
	/// <summary>
	/// The pure edit a completion derives from its snapshot: the full accept (the span replaced with the insertion plus a
	/// trailing space), the one-character inline accept (one ghost character committed, one tail character consumed) and
	/// the ghost preview both share.
	/// </summary>
	public class InputCompletionResultTests
	{
		private static InputCompletionResult Result(string text, int caretIndex, InputCompletionSpan span,
			params string[] insertTexts) => new()
			{
				Text = text,
				CaretIndex = caretIndex,
				Span = span,
				Items = [.. insertTexts.Select(insertText => new InputCompletionItem { InsertText = insertText })]
			};

		private static InputCompletionAccept? Apply(string text, int caretIndex, InputCompletionSpan span,
			string insertText, bool oneChar)
		{
			var result = Result(text, caretIndex, span, insertText);
			return result.Apply(result.Items[0], oneChar);
		}

		private static InputCompletionAccept? ApplyOneChar(string text, int caretIndex, InputCompletionSpan span, string insertText)
			=> Apply(text, caretIndex, span, insertText, oneChar: true);

		private static InputCompletionAccept? ApplyFull(string text, int caretIndex, InputCompletionSpan span, string insertText)
			=> Apply(text, caretIndex, span, insertText, oneChar: false);

		// --- full accept -----------------------------------------------------------------------------------------------

		[Fact]
		public void Apply_Full_ReplacesTheSpan_AndAppendsATrailingSpace()
		{
			var accepted = ApplyFull("/gr", 3, new InputCompletionSpan(0, 3), "/skill:grilling");

			Assert.NotNull(accepted);
			Assert.Equal("/skill:grilling ", accepted!.Value.Text);
			Assert.Equal(16, accepted.Value.Caret);
		}

		// --- one-character (inline) accept ----------------------------------------------------------------------------

		[Fact]
		public void Apply_OneChar_AtTheRegionEnd_InsertsWithoutConsuming()
		{
			var accepted = ApplyOneChar("abc", 3, new InputCompletionSpan(0, 3), "abc123");

			Assert.NotNull(accepted);
			Assert.Equal("abc1", accepted!.Value.Text);
			Assert.Equal(4, accepted.Value.Caret);
		}

		[Fact]
		public void Apply_OneChar_InsideTheRegion_ConsumesOneCharacter()
		{
			// "abc|d e f" → "abc1| e f"
			var accepted = ApplyOneChar("abcd e f", 3, new InputCompletionSpan(0, 4), "abc1 2 3");

			Assert.NotNull(accepted);
			Assert.Equal("abc1 e f", accepted!.Value.Text);
			Assert.Equal(4, accepted.Value.Caret);
		}

		[Fact]
		public void Apply_OneChar_WhenTheCaretIsPastTheRegion_DoesNotConsume()
		{
			var accepted = ApplyOneChar("abc def", 5, new InputCompletionSpan(0, 3), "abc1");

			Assert.NotNull(accepted);
			Assert.Equal("abc d1ef", accepted!.Value.Text);
			Assert.Equal(6, accepted.Value.Caret);
		}

		[Fact]
		public void Apply_OneChar_WhenTheItemDoesNotContinueTheTypedText_ReturnsNull()
		{
			// "/sk" matched by its namespace: the offered "/grilling" does not extend what is typed.
			Assert.Null(ApplyOneChar("/sk", 3, new InputCompletionSpan(0, 3), "/grilling"));
		}

		[Fact]
		public void Apply_OneChar_WhenTheGhostIsEmpty_ReturnsNull()
		{
			Assert.Null(ApplyOneChar("/grilling", 9, new InputCompletionSpan(0, 9), "/grilling"));
		}

		// --- ghost ----------------------------------------------------------------------------------------------------

		[Fact]
		public void GhostOf_IsTheSuffixTheTypedTextDoesNotCarryYet()
		{
			var result = Result("/gr", 3, new InputCompletionSpan(0, 3), "/grilling");

			Assert.Equal("illing", result.GhostOf(result.Items[0]));
		}

		[Fact]
		public void GhostOf_WhenTheItemDoesNotContinueTheTypedText_IsNull()
		{
			var result = Result("/sk", 3, new InputCompletionSpan(0, 3), "/grilling");

			Assert.Null(result.GhostOf(result.Items[0]));
		}

		// --- item access ----------------------------------------------------------------------------------------------

		[Fact]
		public void ItemAt_ReturnsTheItem_OrNullWhenOutOfRange()
		{
			var result = Result("a", 1, new InputCompletionSpan(0, 1), "one", "two");

			Assert.Equal("one", result.ItemAt(0)!.InsertText);
			Assert.Equal("two", result.ItemAt(1)!.InsertText);
			Assert.Null(result.ItemAt(2));
			Assert.Null(result.ItemAt(-1));
		}
	}
}
