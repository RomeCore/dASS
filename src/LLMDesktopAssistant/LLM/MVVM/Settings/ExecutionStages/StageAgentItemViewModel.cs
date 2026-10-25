using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.LLM.MVVM.Settings.ExecutionStages;

public abstract class StageAgentItemViewModelBase : NotifyPropertyChanged
{
	public required ChatAgentDescriptor Agent { get; init; }
	public required ChatAgentInstance Instance { get; init; }
}

/// <summary>
/// ViewModel for an agent inside a sequential stage.
/// </summary>
public class SequentialStageAgentViewModel : StageAgentItemViewModelBase
{
	public IRelayCommand MoveUpCommand { get; }
	public IRelayCommand MoveDownCommand { get; }
	public IRelayCommand RemoveCommand { get; }

	public SequentialStageAgentViewModel(
		Action<SequentialStageAgentViewModel> onMoveUp,
		Action<SequentialStageAgentViewModel> onMoveDown,
		Action<SequentialStageAgentViewModel> onRemove)
	{
		MoveUpCommand = new RelayCommand<SequentialStageAgentViewModel>(_ => onMoveUp(this));
		MoveDownCommand = new RelayCommand<SequentialStageAgentViewModel>(_ => onMoveDown(this));
		RemoveCommand = new RelayCommand<SequentialStageAgentViewModel>(_ => onRemove(this));
	}
}

/// <summary>
/// ViewModel for an agent inside a random stage.
/// </summary>
public class RandomStageAgentViewModel : StageAgentItemViewModelBase
{
	public IRelayCommand RemoveCommand { get; }

	public RandomStageAgentViewModel(
		Action<RandomStageAgentViewModel> onRemove)
	{
		RemoveCommand = new RelayCommand<RandomStageAgentViewModel>(_ => onRemove(this));
	}
}

/// <summary>
/// ViewModel for an agent inside a round-robin stage.
/// Supports ordering (move up/down) and weight display.
/// </summary>
public class RoundRobinStageAgentViewModel : StageAgentItemViewModelBase
{
	public IRelayCommand MoveUpCommand { get; }
	public IRelayCommand MoveDownCommand { get; }
	public IRelayCommand RemoveCommand { get; }

	public RoundRobinStageAgentViewModel(
		Action<RoundRobinStageAgentViewModel> onMoveUp,
		Action<RoundRobinStageAgentViewModel> onMoveDown,
		Action<RoundRobinStageAgentViewModel> onRemove)
	{
		MoveUpCommand = new RelayCommand<RoundRobinStageAgentViewModel>(_ => onMoveUp(this));
		MoveDownCommand = new RelayCommand<RoundRobinStageAgentViewModel>(_ => onMoveDown(this));
		RemoveCommand = new RelayCommand<RoundRobinStageAgentViewModel>(_ => onRemove(this));
	}
}

/// <summary>
/// ViewModel for an agent inside a mention-only stage or adaptive stage.
/// No weight, no ordering - just enabled/disabled with remove.
/// </summary>
public class MentionStageAgentViewModel : StageAgentItemViewModelBase
{
	public IRelayCommand RemoveCommand { get; }

	public MentionStageAgentViewModel(
		Action<MentionStageAgentViewModel> onRemove)
	{
		RemoveCommand = new RelayCommand<MentionStageAgentViewModel>(_ => onRemove(this));
	}
}
