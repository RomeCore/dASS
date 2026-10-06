using LLMDesktopAssistant.Settings.Application;

namespace LLMDesktopAssistant.Tests;

public static class AppSettingsLock
{
	private static readonly SemaphoreSlim _semaphore = new(1, 1);

	public static void Lock(Action<ApplicationSettings> func)
	{
		_semaphore.Wait();
		try
		{
			var appSettings = new ApplicationSettings();
			ApplicationSettingsAccessor.SetApplicationSettings(appSettings);
			func(appSettings);
		}
		finally
		{
			// Reset the settings BEFORE releasing the lock: releasing first opens a window where the
			// next locked test installs its own settings and this cleanup then clobbers them.
			ApplicationSettingsAccessor.SetApplicationSettings(new());
			_semaphore.Release();
		}
	}

	public static T Lock<T>(Func<ApplicationSettings, T> func)
	{
		_semaphore.Wait();
		try
		{
			var appSettings = new ApplicationSettings();
			ApplicationSettingsAccessor.SetApplicationSettings(appSettings);
			return func(appSettings);
		}
		finally
		{
			// Reset the settings BEFORE releasing the lock: releasing first opens a window where the
			// next locked test installs its own settings and this cleanup then clobbers them.
			ApplicationSettingsAccessor.SetApplicationSettings(new());
			_semaphore.Release();
		}
	}

	public static async Task LockAsync(Func<ApplicationSettings, CancellationToken, Task> func, CancellationToken cancellationToken = default)
	{
		await _semaphore.WaitAsync(cancellationToken);
		try
		{
			var appSettings = new ApplicationSettings();
			ApplicationSettingsAccessor.SetApplicationSettings(appSettings);
			await func(appSettings, cancellationToken);
		}
		finally
		{
			// Reset the settings BEFORE releasing the lock: releasing first opens a window where the
			// next locked test installs its own settings and this cleanup then clobbers them.
			ApplicationSettingsAccessor.SetApplicationSettings(new());
			_semaphore.Release();
		}
	}

	public static async Task<T> LockAsync<T>(Func<ApplicationSettings, CancellationToken, Task<T>> func, CancellationToken cancellationToken = default)
	{
		await _semaphore.WaitAsync(cancellationToken);
		try
		{
			var appSettings = new ApplicationSettings();
			ApplicationSettingsAccessor.SetApplicationSettings(appSettings);
			return await func(appSettings, cancellationToken);
		}
		finally
		{
			// Reset the settings BEFORE releasing the lock: releasing first opens a window where the
			// next locked test installs its own settings and this cleanup then clobbers them.
			ApplicationSettingsAccessor.SetApplicationSettings(new());
			_semaphore.Release();
		}
	}
}