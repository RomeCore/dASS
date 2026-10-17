using System.ComponentModel;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Implementations
{
	[ToolModule(chatScoped: false)]
	public class TimeToolModule : ToolModule
	{
		public TimeToolModule()
		{
			AddTool(new ToolInitializationInfo
			{
				Executor = TimeGet,
				Name = "time-get",
				Description = "Gets the current time in the specified timezone.",
				NameKey = Locale.GetKey("tool.name.time-get"),
				DescriptionKey = Locale.GetKey("tool.description.time-get"),
				CategoryKey = Locale.GetKey("tool.category.time")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = TimeWait,
				Name = "time-wait",
				Description = "Waits for a specified duration with real-time progress updates.",
				NameKey = Locale.GetKey("tool.name.time-wait"),
				DescriptionKey = Locale.GetKey("tool.description.time-wait"),
				CategoryKey = Locale.GetKey("tool.category.time"),
				DefaultExpectedBehaviour = ToolBehaviour.LongRunningTask
			});
		}

		private ToolExecutionResult TimeGet(IToolExecutionContext ctx,
			[Description("Timezone to get the time for (e.g., 'UTC', 'UTC+5', 'GMT+3'). Default is current user's timezone.")]
			string? timezone = null)
		{
			try
			{
				TimeZoneInfo tz;
				DateTime time;
				var timezoneId = timezone?.Trim().ToUpperInvariant();

				if (timezoneId == null)
				{
					time = DateTime.Now;
					ctx.Call.SetResult(time.ToString("O"));
					ctx.Call.SetStatus(MaterialIconKind.ClockOutline, time.ToString("yyyy-MM-dd HH:mm:ss.fff"));
					return ToolExecutionResult.Succeeded;
				}

				// Parse timezone from the format "UTC+X", "UTC-X", "GMT+X", "GMT-X" or standard timezone names
				if (timezoneId == "UTC" || timezoneId == "GMT")
				{
					tz = TimeZoneInfo.Utc;
				}
				else if (timezoneId.StartsWith("UTC+") || timezoneId.StartsWith("UTC-") ||
						 timezoneId.StartsWith("GMT+") || timezoneId.StartsWith("GMT-"))
				{
					// Extract the offset
					string offsetStr = timezoneId.Substring(3); // Remove "UTC" or "GMT"
					if (int.TryParse(offsetStr, out int hours))
					{
						try
						{
							tz = TimeZoneInfo.CreateCustomTimeZone(
								timezoneId,
								TimeSpan.FromHours(hours),
								timezoneId,
								timezoneId);
						}
						catch
						{
							ctx.Call.SetResult($"Invalid timezone offset: {timezoneId}");
							return ToolExecutionResult.Failed;
						}
					}
					else
					{
						ctx.Call.SetResult($"Invalid timezone format: {timezone}. Expected format: UTC+X, UTC-X, GMT+X, GMT-X");
						return ToolExecutionResult.Failed;
					}
				}
				else
				{
					// Try to find the timezone by ID
					try
					{
						tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
					}
					catch (TimeZoneNotFoundException)
					{
						ctx.Call.SetResult($"Timezone not found: {timezone}. Valid examples: 'UTC', 'UTC+5', 'GMT+3', 'Pacific Standard Time'");
						return ToolExecutionResult.Failed;
					}
				}

				time = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
				var display = time.ToString("yyyy-MM-dd HH:mm:ss.fff") + $" ({timezoneId})";

				ctx.Call.SetResult(time.ToString("O"));
				ctx.Call.SetStatus(MaterialIconKind.ClockOutline, display);
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetResult($"Time get error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private async Task<ToolExecutionResult> TimeWait(IToolExecutionContext ctx,
			[Description("Duration to wait in format 'hours:minutes:seconds' or 'minutes:seconds' (e.g., '0:05', '1:30', '1:00:10')")]
			string span = "0:00",
			CancellationToken cancellationToken = default)
		{
			try
			{
				// Parse the time span
				if (!TryParseTimeSpan(span, out TimeSpan duration))
				{
					ctx.Call.SetResult("Invalid time span format. Expected format: 'hours:minutes:seconds' or 'minutes:seconds' (e.g., '0:05', '1:30', '0:00:10')");
					return ToolExecutionResult.Failed;
				}

				if (duration.TotalMilliseconds <= 0)
				{
					ctx.Call.SetResult("Wait completed instantly (duration <= 0).");
					ctx.Call.SetStatus(Material.Icons.MaterialIconKind.TimerCheck,
						string.Format(LocalizationManager.LocalizeStatic("tool.status.time-wait.completed"), "0:00"));
					return ToolExecutionResult.Succeeded;
				}

				try
				{
					var stopwatch = System.Diagnostics.Stopwatch.StartNew();
					var totalMilliseconds = duration.TotalMilliseconds;
					var updateInterval = TimeSpan.FromMilliseconds(100); // Update every 100ms for smooth progress

					ctx.Call.SetStatus(MaterialIconKind.TimerSand,
						string.Format(LocalizationManager.LocalizeStatic("tool.status.time-wait.status"), FormatTimeSpan(duration)));
					ctx.Call.SetProgress(0, 0, totalMilliseconds);

					while (stopwatch.Elapsed < duration)
					{
						cancellationToken.ThrowIfCancellationRequested();

						var elapsed = stopwatch.Elapsed;
						var remaining = duration - elapsed;

						ctx.Call.SetProgress(Math.Min(elapsed.TotalMilliseconds, totalMilliseconds), 0, totalMilliseconds);
						ctx.Call.SetStatusTitle(string.Format(LocalizationManager.LocalizeStatic("tool.status.time-wait.status"), FormatTimeSpan(remaining)));
						ctx.Call.SetStatusIcon(MaterialIconKind.TimerSand);

						// Calculate the next update delay, but don't wait longer than remaining time
						var nextUpdateDelay = TimeSpan.FromMilliseconds(Math.Min(
							updateInterval.TotalMilliseconds,
							Math.Max(1, remaining.TotalMilliseconds)));

						await Task.Delay(nextUpdateDelay, cancellationToken);
					}

					stopwatch.Stop();

					ctx.Call.SetResult($"Wait completed: {FormatTimeSpan(stopwatch.Elapsed)} elapsed");
					ctx.Call.SetProgress(totalMilliseconds, 0, totalMilliseconds);
					ctx.Call.SetStatus(MaterialIconKind.TimerCheck,
						string.Format(LocalizationManager.LocalizeStatic("tool.status.time-wait.completed"), FormatTimeSpan(stopwatch.Elapsed)));
					return ToolExecutionResult.Succeeded;
				}
				catch (OperationCanceledException)
				{
					ctx.Call.SetStatus(MaterialIconKind.TimerCancel, "Wait cancelled");
					return ToolExecutionResult.Failed;
				}
				catch (Exception ex)
				{
					ctx.Call.SetStatus(null, $"Error: {ex.Message}");
					return ToolExecutionResult.Failed;
				}
			}
			catch (Exception ex)
			{
				ctx.Call.SetResult($"Time wait error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private bool TryParseTimeSpan(string span, out TimeSpan result)
		{
			result = TimeSpan.Zero;

			if (string.IsNullOrWhiteSpace(span))
				return false;

			// Try parsing "MM:SS" or "H:MM:SS" format with flexible digits
			var parts = span.Split(':');

			if (parts.Length == 2)
			{
				// Format: minutes:seconds
				if (int.TryParse(parts[0], out int minutes) &&
					int.TryParse(parts[1], out int seconds))
				{
					if (minutes >= 0 && seconds >= 0)
					{
						result = new TimeSpan(0, minutes, seconds);
						return true;
					}
				}
			}
			else if (parts.Length == 3)
			{
				// Format: hours:minutes:seconds
				if (int.TryParse(parts[0], out int hours) &&
					int.TryParse(parts[1], out int minutes) &&
					int.TryParse(parts[2], out int seconds))
				{
					if (hours >= 0 && minutes >= 0 && seconds >= 0)
					{
						result = new TimeSpan(hours, minutes, seconds);
						return true;
					}
				}
			}

			return false;
		}

		private string FormatTimeSpan(TimeSpan span)
		{
			if (span.TotalHours >= 1)
				return $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}";
			return $"{span.Minutes:D2}:{span.Seconds:D2}";
		}
	}
}