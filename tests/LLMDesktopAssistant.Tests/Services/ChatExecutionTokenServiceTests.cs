using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Tests.Services
{
	public class ChatExecutionTokenServiceTests
	{
		[Fact]
		public void Idle_HasNoExecutionToken()
		{
			var service = new ChatExecutionTokenService();

			Assert.Null(service.ExecutionCancellationToken);
		}

		[Fact]
		public void WithToken_CreatesAndReleasesTheSharedToken()
		{
			var service = new ChatExecutionTokenService();
			var changes = 0;
			service.ExecutionCancellationTokenChanged += () => changes++;

			using (service.WithToken(ChatExecutionLevel.Operation, default, out var token))
			{
				Assert.NotNull(service.ExecutionCancellationToken);
				Assert.False(token.IsCancellationRequested);
			}

			Assert.Null(service.ExecutionCancellationToken);
			Assert.Equal(2, changes);
		}

		[Fact]
		public void WithToken_LevelsMayBeSkipped()
		{
			var service = new ChatExecutionTokenService();

			using (service.WithToken(ChatExecutionLevel.Message, default, out var token))
			{
				Assert.NotNull(service.ExecutionCancellationToken);
				Assert.False(token.IsCancellationRequested);
			}

			Assert.Null(service.ExecutionCancellationToken);
		}

		[Fact]
		public void TakingSameLevelTwice_ReplacesTheFirstToken()
		{
			var service = new ChatExecutionTokenService();

			using var first = service.WithToken(ChatExecutionLevel.Operation, default, out var firstToken);
			using var second = service.WithToken(ChatExecutionLevel.Operation, default, out var secondToken);

			Assert.True(firstToken.IsCancellationRequested);
			Assert.False(secondToken.IsCancellationRequested);
		}

		[Fact]
		public void WiderLevel_CancelsNarrowerLevels()
		{
			var service = new ChatExecutionTokenService();

			using var operation = service.WithToken(ChatExecutionLevel.Operation, default, out var operationToken);
			using var message = service.WithToken(ChatExecutionLevel.Message, default, out var messageToken);

			Assert.False(operationToken.IsCancellationRequested);
			Assert.False(messageToken.IsCancellationRequested);

			using var replacement = service.WithToken(ChatExecutionLevel.Operation, default, out var replacementToken);

			Assert.True(operationToken.IsCancellationRequested);
			Assert.True(messageToken.IsCancellationRequested);
			Assert.False(replacementToken.IsCancellationRequested);
		}

		[Fact]
		public void DisposingNarrower_DoesNotCancelWider()
		{
			var service = new ChatExecutionTokenService();

			using var operation = service.WithToken(ChatExecutionLevel.Operation, default, out var operationToken);
			using (service.WithToken(ChatExecutionLevel.Message, default, out _))
			{
			}

			Assert.False(operationToken.IsCancellationRequested);
			Assert.NotNull(service.ExecutionCancellationToken);
		}

		[Fact]
		public void DisposingWider_CancelsNarrower()
		{
			var service = new ChatExecutionTokenService();

			var operation = service.WithToken(ChatExecutionLevel.Operation, default, out _);
			using var message = service.WithToken(ChatExecutionLevel.Message, default, out var messageToken);

			operation.Dispose();

			Assert.True(messageToken.IsCancellationRequested);
			Assert.Null(service.ExecutionCancellationToken);
		}

		[Fact]
		public void TryCancel_CancelsLevelAndNarrower_AndReportsWhetherActive()
		{
			var service = new ChatExecutionTokenService();

			using var operation = service.WithToken(ChatExecutionLevel.Operation, default, out var operationToken);
			using var message = service.WithToken(ChatExecutionLevel.Message, default, out var messageToken);

			Assert.True(service.TryCancel(ChatExecutionLevel.Operation));
			Assert.True(operationToken.IsCancellationRequested);
			Assert.True(messageToken.IsCancellationRequested);
			Assert.Null(service.ExecutionCancellationToken);

			Assert.False(service.TryCancel(ChatExecutionLevel.Operation));
		}

		[Fact]
		public void SharedToken_CancelCancelsEveryLevel()
		{
			var service = new ChatExecutionTokenService();

			using var operation = service.WithToken(ChatExecutionLevel.Operation, default, out var operationToken);
			using var message = service.WithToken(ChatExecutionLevel.Message, default, out var messageToken);

			service.ExecutionCancellationToken!.Cancel();

			Assert.True(operationToken.IsCancellationRequested);
			Assert.True(messageToken.IsCancellationRequested);
		}

		[Fact]
		public void AfterCancelEverything_ANewLevelGetsAFreshToken()
		{
			var service = new ChatExecutionTokenService();

			var stale = service.WithToken(ChatExecutionLevel.Operation, default, out _);
			service.ExecutionCancellationToken!.Cancel();

			using (service.WithToken(ChatExecutionLevel.Operation, default, out var token))
				Assert.False(token.IsCancellationRequested);

			stale.Dispose();
		}

		[Fact]
		public void InputCancellation_FlowsIntoTheLevelToken()
		{
			var service = new ChatExecutionTokenService();

			using var input = new CancellationTokenSource();
			using (service.WithToken(ChatExecutionLevel.Operation, input.Token, out var token))
			{
				input.Cancel();
				Assert.True(token.IsCancellationRequested);
			}
		}
	}
}
