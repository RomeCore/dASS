using System.Collections.Immutable;
using LLMDesktopAssistant.Utils;
using Serilog;
using Serilog.Events;

namespace LLMDesktopAssistant.Tests.Utils
{
	public class LogBufferTests
	{
		private static readonly ILogger _logger = new LoggerConfiguration()
			.MinimumLevel.Verbose()
			.WriteTo.Sink(LogBuffer.Sink)
			.CreateLogger();

		private static string Marker() => "logbuf-" + Guid.NewGuid().ToString("N");

		[Fact]
		public void Sequence_IsStrictlyIncreasingAcrossEmits()
		{
			var marker = Marker();
			var baseline = LogBuffer.Count;

			for (int index = 0; index < 20; index++)
				_logger.Information("{Marker} {Index}", marker, index);

			var entries = LogBuffer.GetAfter(baseline);

			Assert.True(entries.Count >= 20);
			for (int index = 1; index < entries.Count; index++)
				Assert.True(entries[index].Sequence > entries[index - 1].Sequence);
		}

		[Fact]
		public void GetAfter_ReturnsOnlyNewerEntries()
		{
			var marker = Marker();

			_logger.Information("{Marker} before", marker);
			var last = LogBuffer.Count;
			_logger.Information("{Marker} after-one", marker);
			_logger.Information("{Marker} after-two", marker);

			var entries = LogBuffer.GetAfter(last).Where(entry => entry.Rendered.Contains(marker)).ToList();

			Assert.Equal(2, entries.Count);
			Assert.Contains("after-one", entries[0].Rendered);
			Assert.Contains("after-two", entries[1].Rendered);
		}

		[Fact]
		public void GetAfter_CarriesTheRenderedTemplateAndProperties()
		{
			var marker = Marker();
			var baseline = LogBuffer.Count;

			_logger.Warning("User {Name} failed with {Marker}", "John", marker);

			var entry = Assert.Single(LogBuffer.GetAfter(baseline), item => item.Rendered.Contains(marker));

			Assert.Equal(LogEventLevel.Warning, entry.Level);
			Assert.Equal("User {Name} failed with {Marker}", entry.Template);
			Assert.Equal("John", entry.Properties["Name"]);
			Assert.Equal(marker, entry.Properties["Marker"]);
			Assert.True(entry.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-1));
			Assert.Null(entry.Exception);
		}

		[Fact]
		public void GetAfter_CarriesTheException()
		{
			var marker = Marker();
			var baseline = LogBuffer.Count;

			_logger.Error(new InvalidOperationException(marker), "failed");

			var entry = Assert.Single(LogBuffer.GetAfter(baseline), item => item.Exception?.Message == marker);

			Assert.IsType<InvalidOperationException>(entry.Exception);
			Assert.Equal(marker, entry.Exception!.Message);
		}

		[Fact]
		public void Emitted_IsRaisedWithTheStoredEntry()
		{
			var marker = Marker();
			var seen = new List<RenderedLogEvent>();

			void OnEmitted(RenderedLogEvent entry) => seen.Add(entry);

			LogBuffer.Emitted += OnEmitted;
			try
			{
				_logger.Information("{Marker} emitted", marker);
			}
			finally
			{
				LogBuffer.Emitted -= OnEmitted;
			}

			var captured = Assert.Single(seen, entry => entry.Rendered.Contains(marker));
			var stored = Assert.Single(LogBuffer.GetAfter(captured.Sequence - 1),
				entry => entry.Sequence == captured.Sequence);

			Assert.Equal(captured.Sequence, stored.Sequence);
			Assert.Equal(captured.Rendered, stored.Rendered);
		}

		[Fact]
		public void Ring_EvictsTheOldestAndKeepsTheSequenceContiguous()
		{
			var marker = Marker();

			for (int index = 0; index < LogBuffer.MaxBufferSize + 100; index++)
				_logger.Information(marker + " " + index);

			var entries = LogBuffer.GetAfter(0);

			Assert.Equal(LogBuffer.MaxBufferSize, entries.Count);
			for (int index = 1; index < entries.Count; index++)
				Assert.Equal(entries[index - 1].Sequence + 1, entries[index].Sequence);

			Assert.Contains(entries, entry => entry.Rendered.Contains($"{marker} 5099"));
			Assert.DoesNotContain(entries, entry => entry.Rendered.Contains($"{marker} 0"));
		}

		[Fact]
		public void Ring_DoesNotThrowAfterTheSecondWrap()
		{
			var marker = Marker();

			// Two full rings: the first wrap is where the write pointer must start over.
			for (int index = 0; index < LogBuffer.MaxBufferSize * 2 + 10; index++)
				_logger.Information(marker + " " + index);

			var entries = LogBuffer.GetAfter(0);

			Assert.Equal(LogBuffer.MaxBufferSize, entries.Count);
			Assert.Contains(entries, entry => entry.Rendered.Contains($"{marker} 10009"));
		}
	}
}
