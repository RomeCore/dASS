using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MVVM.Dynamic;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.MVVM.Debug;

/// <summary>
/// View model for the dynamic view model debug page: edits an AXAML fragment that is loaded at
/// runtime with reflection bindings and rendered against a <see cref="DictionaryDynamicViewModel"/>,
/// while the members of that view model can be mutated to observe live binding updates.
/// </summary>
[ViewModelFor(typeof(DynamicViewModelDebugPageView))]
public class DynamicViewModelDebugPageViewModel : ViewModelBase
{
	private const int MaxLogEntries = 200;

	private const string DefaultXaml = """
		<StackPanel xmlns="https://github.com/avaloniaui"
					Spacing="6" Margin="4">
			<TextBlock Text="{Binding text}" FontSize="18" FontWeight="Bold"/>
			<TextBlock Text="{Binding [text]}" Foreground="Gray"/>
			<TextBox Text="{Binding text, Mode=TwoWay}" PlaceholderText="TwoWay -> text"/>
			<CheckBox IsChecked="{Binding enabled, Mode=TwoWay}" Content="enabled"/>
			<Button Content="Click me" Command="{Binding clickCommand}"/>
			<TextBlock Text="{Binding lateKey}" Foreground="MediumSeaGreen" FontWeight="Bold"/>
		</StackPanel>
		""";

	/// <summary>
	/// Gets the dynamic view model used as the data context of the loaded preview.
	/// </summary>
	public DictionaryDynamicViewModel Vm { get; } = new();

	private string _xaml = DefaultXaml;
	/// <summary>
	/// Gets or sets the AXAML fragment that is compiled and rendered at runtime.
	/// </summary>
	public string Xaml
	{
		get => _xaml;
		set => SetProperty(ref _xaml, value);
	}

	private Control? _preview;
	/// <summary>
	/// Gets the control produced by the last successful load, or <see langword="null"/>.
	/// </summary>
	public Control? Preview
	{
		get => _preview;
		private set => SetProperty(ref _preview, value);
	}

	private string? _loadError;
	/// <summary>
	/// Gets the error produced by the last load attempt, if any.
	/// </summary>
	public string? LoadError
	{
		get => _loadError;
		private set => SetProperty(ref _loadError, value);
	}

	private string _key = "text";
	/// <summary>
	/// Gets or sets the key written by <see cref="SetCommand"/>.
	/// </summary>
	public string Key
	{
		get => _key;
		set => SetProperty(ref _key, value);
	}

	private string _value = "";
	/// <summary>
	/// Gets or sets the value written by <see cref="SetCommand"/>.
	/// </summary>
	public string Value
	{
		get => _value;
		set => SetProperty(ref _value, value);
	}

	/// <summary>
	/// Gets the log of change notifications raised by the dynamic view model.
	/// </summary>
	public RangeObservableCollection<string> Log { get; } = [];

	/// <summary>
	/// Gets the command that loads the current AXAML into the preview.
	/// </summary>
	public IRelayCommand LoadCommand { get; }

	/// <summary>
	/// Gets the command that restores the sample AXAML and reloads it.
	/// </summary>
	public IRelayCommand ResetXamlCommand { get; }

	/// <summary>
	/// Gets the command that writes <see cref="Key"/> / <see cref="Value"/> into the view model.
	/// </summary>
	public IRelayCommand SetCommand { get; }

	/// <summary>
	/// Gets the command that randomizes a couple of members to observe binding updates.
	/// </summary>
	public IRelayCommand RandomizeCommand { get; }

	/// <summary>
	/// Gets the command that creates a member that did not exist at load time.
	/// </summary>
	public IRelayCommand AddLateKeyCommand { get; }

	/// <summary>
	/// Gets the command that clears the late key member.
	/// </summary>
	public IRelayCommand ClearLateKeyCommand { get; }

	/// <summary>
	/// Gets the command that clears <see cref="Log"/>.
	/// </summary>
	public IRelayCommand ClearLogCommand { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicViewModelDebugPageViewModel"/> class.
	/// </summary>
	public DynamicViewModelDebugPageViewModel()
	{
		Vm.PropertyChanged += (_, e) => AppendLog($"PropertyChanged(\"{e.PropertyName}\")");
		Vm.CollectionChanged += (_, e) => AppendLog($"CollectionChanged({e.Action})");

		Vm.SetDynamicMember("text", "Dynamic value");
		Vm.SetDynamicMember("enabled", true);
		Vm.SetDynamicMember("clickCommand", new RelayCommand(() => AppendLog("clickCommand executed")));

		LoadCommand = new RelayCommand(LoadXaml);
		ResetXamlCommand = new RelayCommand(() =>
		{
			Xaml = DefaultXaml;
			LoadXaml();
		});
		SetCommand = new RelayCommand(ApplyKeyValue);
		RandomizeCommand = new RelayCommand(Randomize);
		AddLateKeyCommand = new RelayCommand(() => SetMember("lateKey", $"activated at {DateTime.Now:HH:mm:ss}"));
		ClearLateKeyCommand = new RelayCommand(() => SetMember("lateKey", null));
		ClearLogCommand = new RelayCommand(() => Log.Clear());

		LoadXaml();
	}

	private void ApplyKeyValue()
	{
		if (string.IsNullOrWhiteSpace(Key))
			return;
		SetMember(Key, Value);
	}

	private void SetMember(string key, object? value)
	{
		Vm.SetDynamicMember(key, value);
		AppendLog($"SetDynamicMember(\"{key}\", {value?.ToString() ?? "null"})");
	}

	private void Randomize()
	{
		SetMember("text", $"changed #{Random.Shared.Next(1000)}");
		SetMember("enabled", Random.Shared.Next(2) == 0);
	}

	private void AppendLog(string message)
	{
		Log.Add(message);
		while (Log.Count > MaxLogEntries)
			Log.RemoveAt(0);
	}

	private void LoadXaml()
	{
		Preview = null;
		LoadError = null;

		try
		{
			var document = new RuntimeXamlLoaderDocument(
				new Uri("avares://LLMDesktopAssistant/MVVM/Debug/DynamicPreview.axaml"),
				Xaml);
			var configuration = new RuntimeXamlLoaderConfiguration
			{
				LocalAssembly = typeof(DynamicViewModelDebugPageViewModel).Assembly,
				UseCompiledBindingsByDefault = false,
			};

			var root = AvaloniaRuntimeXamlLoader.Load(document, configuration);
			if (root is Control control)
			{
				control.DataContext = Vm;
				Preview = control;
			}
			else
			{
				LoadError = Locale.Get("debug.dynamic_viewmodel.error.not_control");
			}
		}
		catch (Exception ex)
		{
			LoadError = ex.Message;
		}
	}
}
