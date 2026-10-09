using System;
using System.Collections.Generic;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Tests.InputCompletion
{
	/// <summary>
	/// The general input-completion core: the session (the single active source and its kept result), the source
	/// contract and the model types.
	/// </summary>
	public class InputCompletionServiceTests
	{
		private sealed class FakeSource(int priority, Func<string, int, InputCompletionResult?> compute)
			: IInputCompletionSource
		{
			public int Priority { get; } = priority;
			public int Calls { get; private set; }
			public string? LastText { get; private set; }
			public int LastCaret { get; private set; }

			public InputCompletionResult? TryCompute(string text, int caretIndex)
			{
				Calls++;
				LastText = text;
				LastCaret = caretIndex;
				return compute(text, caretIndex);
			}

			public IReadOnlyList<TextHighlightSpan>? TryHighlight(string text, int caretIndex) => null;
		}

		private static InputCompletionResult Result(string insertText, LocaleKeyBase? title = null,
			IReadOnlyList<InputCompletionItem>? items = null)
			=> new()
			{
				Text = "/gr",
				CaretIndex = 3,
				Span = new InputCompletionSpan(0, 1),
				State = title is null ? null : new InputCompletionState { Title = title },
				Items = items ?? [new InputCompletionItem { InsertText = insertText }]
			};

		private static FakeSource Claims(int priority, string insertText)
			=> new(priority, (_, _) => Result(insertText));

		private static FakeSource DoesNotClaim(int priority)
			=> new(priority, (_, _) => null);

		[Fact]
		public void Update_WithNoSources_LeavesNoResult()
		{
			var service = new InputCompletionService([]);

			service.Update("/gr", 3);

			Assert.Null(service.Result);
		}

		[Fact]
		public void Update_WhenNoSourceClaims_LeavesNoResult()
		{
			var source = DoesNotClaim(0);
			var service = new InputCompletionService([source]);

			service.Update("hello", 5);

			Assert.Null(service.Result);
			Assert.Equal(1, source.Calls);
		}

		[Fact]
		public void Update_HandsTheTextAndCaretToTheSource()
		{
			var source = DoesNotClaim(0);
			var service = new InputCompletionService([source]);

			service.Update("/gr", 3);

			Assert.Equal("/gr", source.LastText);
			Assert.Equal(3, source.LastCaret);
		}

		[Fact]
		public void Update_PicksTheHighestPriorityClaimingSource()
		{
			var low = Claims(1, "low");
			var high = Claims(10, "high");
			var service = new InputCompletionService([low, high]);

			service.Update("/gr", 3);

			Assert.Equal("high", service.Result!.Items[0].InsertText);
			Assert.Equal(1, high.Calls);
			Assert.Equal(0, low.Calls);
		}

		[Fact]
		public void Update_SkipsANonClaimingSource_AndFallsThrough()
		{
			var high = DoesNotClaim(10);
			var low = Claims(1, "low");
			var service = new InputCompletionService([high, low]);

			service.Update("/gr", 3);

			Assert.Equal("low", service.Result!.Items[0].InsertText);
			Assert.Equal(1, high.Calls);
			Assert.Equal(1, low.Calls);
		}

		[Fact]
		public void Update_OrdersByPriority_RegardlessOfRegistrationOrder()
		{
			var service = new InputCompletionService([Claims(1, "low"), Claims(5, "high")]);

			service.Update("/", 1);

			Assert.Equal("high", service.Result!.Items[0].InsertText);
		}

		[Fact]
		public void Update_OnAPriorityTie_IsDeterministicByTypeName()
		{
			// Two different source types with the same priority: the ordinal type-name tie-break keeps the winner stable
			// regardless of registration order.
			var service = new InputCompletionService([new BetaSource(), new AlphaSource()]);

			service.Update("/", 1);

			Assert.Equal("alpha", service.Result!.Items[0].InsertText);
		}

		private sealed class AlphaSource : IInputCompletionSource
		{
			public int Priority => 5;

			public InputCompletionResult? TryCompute(string text, int caretIndex) => new()
			{
				Text = text,
				CaretIndex = caretIndex,
				Span = new InputCompletionSpan(0, 0),
				Items = [new InputCompletionItem { InsertText = "alpha" }]
			};

			public IReadOnlyList<TextHighlightSpan>? TryHighlight(string text, int caretIndex) => null;
		}

		private sealed class BetaSource : IInputCompletionSource
		{
			public int Priority => 5;

			public InputCompletionResult? TryCompute(string text, int caretIndex) => new()
			{
				Text = text,
				CaretIndex = caretIndex,
				Span = new InputCompletionSpan(0, 0),
				Items = [new InputCompletionItem { InsertText = "beta" }]
			};

			public IReadOnlyList<TextHighlightSpan>? TryHighlight(string text, int caretIndex) => null;
		}

		[Fact]
		public void Update_KeepsAStateWithNoItems()
		{
			var title = Locale.GetKey("command.argument.wait");
			var source = new FakeSource(0, (_, _) => Result("ignored", title, items: []));
			var service = new InputCompletionService([source]);

			service.Update("/agent:x ", 9);

			Assert.NotNull(service.Result);
			Assert.Empty(service.Result!.Items);
			Assert.NotNull(service.Result.State);
			Assert.Equal("command.argument.wait", service.Result.State!.Title!.Key);
		}

		[Fact]
		public void Update_RaisesResultChanged()
		{
			var service = new InputCompletionService([Claims(0, "one")]);
			var raised = 0;
			service.ResultChanged += (_, _) => raised++;

			service.Update("/gr", 3);

			Assert.Equal(1, raised);
		}

		[Fact]
		public void Close_DropsTheResult_AndRaisesResultChanged()
		{
			var service = new InputCompletionService([Claims(0, "one")]);
			service.Update("/gr", 3);

			var raised = 0;
			service.ResultChanged += (_, _) => raised++;

			service.Close();

			Assert.Null(service.Result);
			Assert.Equal(1, raised);
		}

		[Fact]
		public void Close_WhenAlreadyClosed_IsSilent()
		{
			var service = new InputCompletionService([Claims(0, "one")]);
			var raised = 0;
			service.ResultChanged += (_, _) => raised++;

			service.Close();

			Assert.Equal(0, raised);
		}

		[Fact]
		public void Item_DisplayText_FallsBackToInsertText()
		{
			var withoutDisplay = new InputCompletionItem { InsertText = "/skill:grilling" };
			var withDisplay = new InputCompletionItem { InsertText = "/skill:grilling", Display = "grilling" };

			Assert.Equal("/skill:grilling", withoutDisplay.DisplayText);
			Assert.Equal("grilling", withDisplay.DisplayText);
		}

		[Fact]
		public void Span_FromBounds_AndEnd()
		{
			var span = InputCompletionSpan.FromBounds(2, 7);

			Assert.Equal(2, span.Start);
			Assert.Equal(5, span.Length);
			Assert.Equal(7, span.End);
		}
	}
}
