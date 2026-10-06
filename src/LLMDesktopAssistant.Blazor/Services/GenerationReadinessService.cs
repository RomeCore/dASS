using System.Collections.Concurrent;
using System.Collections.Specialized;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Blazor.Services
{
	[WebUIService(typeof(IGenerationReadinessService), IsScoped = false)]
	public class GenerationReadinessService : NotifyPropertyChanged, IGenerationReadinessService
	{
		private readonly ConcurrentDictionary<string, UserReadinessState> _userReadinessStates = [];
		private readonly IOnlineStateService _onlineState;
		private readonly Chat _chat;
		private readonly IChatOperationService _chatOperator;
		private readonly IChatExecutionTokenService _executionTokens;

		private int _totalCount;
		public int TotalCount
		{
			get => _totalCount;
			private set => SetProperty(ref _totalCount, value);
		}

		private int _readyCount;
		public int ReadyCount
		{
			get => _readyCount;
			private set => SetProperty(ref _readyCount, value);
		}

		public GenerationReadinessService(IOnlineStateService onlineState, Chat chat, IChatOperationService chatOperator)
		{
			_onlineState = onlineState ?? throw new ArgumentNullException(nameof(onlineState));
			_chat = chat ?? throw new ArgumentNullException(nameof(chat));
			_chatOperator = chatOperator ?? throw new ArgumentNullException(nameof(chatOperator));
			_executionTokens = _chat.Services.GetRequiredService<IChatExecutionTokenService>();

			_chat.Messages.CollectionChanged += ChatMessagesCollectionChanged;
			OnlineUsers_CollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, _onlineState.OnlineUsers));
			_onlineState.OnlineUsers.CollectionChanged += OnlineUsers_CollectionChanged;
		}

		public UserReadinessState GetReadinessState(string login)
		{
			if (_userReadinessStates.TryGetValue(login, out var result))
				return result;
			result = new UserReadinessState
			{
				Login = login
			};
			result.PropertyChanged += UserReadinessPropertyChanged;
			_userReadinessStates[login] = result;
			return result;
		}

		private void UserReadinessPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			var userReadiness = (UserReadinessState)sender!;
			if (e.PropertyName == nameof(UserReadinessState.IsReady) && _onlineState.OnlineUsers.Contains(userReadiness.Login))
			{
				UpdateReadyCount();
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			_chat.Messages.CollectionChanged -= ChatMessagesCollectionChanged;
			_onlineState.OnlineUsers.CollectionChanged -= OnlineUsers_CollectionChanged;
		}

		private void ChatMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			TryGenerate();
		}

		private void OnlineUsers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null)
			{
				foreach (string newUser in e.NewItems)
				{
					GetReadinessState(newUser);
				}
			}

			UpdateReadyCount();
		}

		private void UpdateReadyCount()
		{
			TotalCount = _onlineState.OnlineUsers.Count;
			ReadyCount = _onlineState.OnlineUsers.Count(u =>
				_userReadinessStates.TryGetValue(u, out var state) && (state.IsReady || state.IsAlwaysReady));
			TryGenerate();
		}

		private void TryGenerate()
		{
			if (_executionTokens.ExecutionCancellationToken != null || _chat.Messages.Count == 0 || _chat.Messages[^1].Message is AssistantMessage)
				return;
			if (ReadyCount < TotalCount || TotalCount == 0)
				return;

			_chatOperator.ContinueGenerationAsync();

			foreach (var user in _onlineState.OnlineUsers)
			{
				if (_userReadinessStates.TryGetValue(user, out var state))
				{
					state.IsReady = state.IsAlwaysReady;
				}
			}
		}
	}
}
