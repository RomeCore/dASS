using System.Diagnostics.CodeAnalysis;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Tests.InputCompletion
{
	/// <summary>
	/// The general input-completion core: the single-active-source resolution and the model types.
	/// </summary>
	public class InputCompletionServiceTests
	{
		private sealed class FakeSource(int priority, Func<InputCompletionRequest, InputCompletionResult?> compute)
			: IInputCompletionSource
		{
			public int Priority { get; } = priority;
			public int Calls { get; private set; }

			public bool TryCompute(InputCompletionRequest request, [NotNullWhen(true)] out InputCompletionResult? result)
			{
				Calls++;
				result = compute(request);
				return result is not null;
			}
		}

		private static InputCompletionResult Result(string insertText, LocaleKeyBase? title = null,
			IReadOnlyList<InputCompletionItem>? items = null, int? selectedIndex = null)
			=> new()
			{
				Span = new InputCompletionSpan(0, 1),
				State = title is null ? null : new InputCompletionState { Title = title },
				Items = items ?? [new InputCompletionItem { InsertText = insertText }],
				SelectedIndex = selectedIndex ?? 0
			};

		private static FakeSource Claims(int priority, string insertText)
			=> new(priority, _ => Result(insertText));

		private static FakeSource DoesNotClaim(int priority)
			=> new(priority, _ => null);

		[Fact]
		public void Compute_WithNoSources_ReturnsNull()
		{
			var service = new InputCompletionService([]);

			Assert.Null(service.Compute(new InputCompletionRequest("/gr", 3)));
		}

		[Fact]
		public void Compute_WhenNoSourceClaims_ReturnsNull()
		{
			var source = DoesNotClaim(0);
			var service = new InputCompletionService([source]);

			Assert.Null(service.Compute(new InputCompletionRequest("hello", 5)));
			Assert.Equal(1, source.Calls);
		}

		[Fact]
		public void Compute_PicksTheHighestPriorityClaimingSource()
		{
			var low = Claims(1, "low");
			var high = Claims(10, "high");
			var service = new InputCompletionService([low, high]);

			var result = service.Compute(new InputCompletionRequest("/gr", 3));

			Assert.Equal("high", result!.Items[0].InsertText);
			Assert.Equal(1, high.Calls);
			Assert.Equal(0, low.Calls);
		}

		[Fact]
		public void Compute_SkipsANonClaimingSource_AndFallsThrough()
		{
			var high = DoesNotClaim(10);
			var low = Claims(1, "low");
			var service = new InputCompletionService([high, low]);

			var result = service.Compute(new InputCompletionRequest("/gr", 3));

			Assert.Equal("low", result!.Items[0].InsertText);
			Assert.Equal(1, high.Calls);
			Assert.Equal(1, low.Calls);
		}

		[Fact]
		public void Compute_OrdersByPriority_RegardlessOfRegistrationOrder()
		{
			var service = new InputCompletionService([Claims(1, "low"), Claims(5, "high")]);

			Assert.Equal("high", service.Compute(new InputCompletionRequest("/", 1))!.Items[0].InsertText);
		}

		[Fact]
		public void Compute_OnAPriorityTie_IsDeterministicByTypeName()
		{
			// Two different source types with the same priority: the ordinal type-name tie-break keeps the winner stable
			// regardless of registration order.
			var service = new InputCompletionService([new BetaSource(), new AlphaSource()]);

			Assert.Equal("alpha", service.Compute(new InputCompletionRequest("/", 1))!.Items[0].InsertText);
		}

		private sealed class AlphaSource : IInputCompletionSource
		{
			public int Priority => 5;

			public bool TryCompute(InputCompletionRequest request, [NotNullWhen(true)] out InputCompletionResult? result)
			{
				result = new InputCompletionResult
				{
					Span = new InputCompletionSpan(0, 0),
					Items = [new InputCompletionItem { InsertText = "alpha" }]
				};
				return true;
			}
		}

		private sealed class BetaSource : IInputCompletionSource
		{
			public int Priority => 5;

			public bool TryCompute(InputCompletionRequest request, [NotNullWhen(true)] out InputCompletionResult? result)
			{
				result = new InputCompletionResult
				{
					Span = new InputCompletionSpan(0, 0),
					Items = [new InputCompletionItem { InsertText = "beta" }]
				};
				return true;
			}
		}

		[Fact]
		public void Compute_ReturnsAStateWithNoItems()
		{
			var title = Locale.GetKey("command.argument.wait");
			var source = new FakeSource(0, _ => Result("ignored", title, items: []));
			var service = new InputCompletionService([source]);

			var result = service.Compute(new InputCompletionRequest("/agent:x ", 9));

			Assert.NotNull(result);
			Assert.Empty(result!.Items);
			Assert.NotNull(result.State);
			Assert.Equal("command.argument.wait", result.State!.Title!.Key);
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
