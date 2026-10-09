using System;
using System.Collections.Generic;
using Avalonia.Input;
using LLMDesktopAssistant.InputCompletion;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>The text and caret a completion accept produces.</summary>
	public readonly record struct InputCompletionAccept(string Text, int Caret);

	/// <summary>
	/// Drives the input completion: it asks <see cref="IInputCompletionService"/> for the completion at the caret and
	/// exposes the state the popup binds to, the current selection and the accept action. It owns no text — the caller
	/// supplies the text and applies the accept.
	/// </summary>
	public sealed class InputCompletionViewModel : NotifyPropertyChanged
	{
		private readonly IInputCompletionService _service;

		private string _text = string.Empty;
		private int _caretIndex;

		public InputCompletionViewModel(IInputCompletionService service)
		{
			_service = service ?? throw new ArgumentNullException(nameof(service));
		}

		/// <summary>The completion computed for the last <see cref="Update"/>, or <see langword="null"/> when none.</summary>
		public InputCompletionResult? Result { get; private set; }

		private bool _isOpen;
		/// <summary>Whether the popup is open (a completion exists, even if it carries no items).</summary>
		public bool IsOpen
		{
			get => _isOpen;
			private set => SetProperty(ref _isOpen, value);
		}

		private IReadOnlyList<InputCompletionItem> _items = [];
		/// <summary>The continuations to show.</summary>
		public IReadOnlyList<InputCompletionItem> Items
		{
			get => _items;
			private set => SetProperty(ref _items, value);
		}

		private int _selectedIndex;
		/// <summary>The index of the selected continuation.</summary>
		public int SelectedIndex
		{
			get => _selectedIndex;
			set
			{
				if (Items.Count == 0)
					return;
				if (SetProperty(ref _selectedIndex, ((value % Items.Count) + Items.Count) % Items.Count))
					RaisePropertyChanged(nameof(SelectedItem));
			}
		}

		/// <summary>The selected continuation, or <see langword="null"/> when there are none.</summary>
		public InputCompletionItem? SelectedItem => Items.Count == 0 ? null : Items[SelectedIndex];

		/// <summary>The state shown above the list (also when the list is empty).</summary>
		public InputCompletionState? State { get; private set; }

		/// <summary>Whether there is nothing to show but the state.</summary>
		public bool IsStateOnly => IsOpen && Items.Count == 0;

		/// <summary>Whether <see cref="Accept"/> can produce a result.</summary>
		public bool CanAccept => IsOpen && SelectedItem is not null && Result is not null;

		/// <summary>
		/// Recomputes the completion for the given text and caret. With no completion the popup closes.
		/// </summary>
		public void Update(string? text, int caretIndex)
		{
			_text = text ?? string.Empty;
			_caretIndex = caretIndex;

			SetResult(_service.Compute(new InputCompletionRequest(_text, caretIndex)));
		}

		/// <summary>Recomputes the completion for the last text and caret.</summary>
		public void Refresh() => Update(_text, _caretIndex);

		/// <summary>Closes the popup without changing the text.</summary>
		public void Close()
		{
			if (IsOpen)
				SetResult(null);
		}

		/// <summary>
		/// Handles the keys the popup owns. Returns <see langword="true"/> when the key was consumed. Enter/Tab are not
		/// handled here — the caller checks <see cref="CanAccept"/> and calls <see cref="Accept"/>.
		/// </summary>
		public bool TryHandleKey(Key key)
		{
			if (!IsOpen)
				return false;

			switch (key)
			{
				case Key.Up:
					Move(-1);
					return true;
				case Key.Down:
					Move(1);
					return true;
				case Key.Escape:
					Close();
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Accepts the selected continuation: it replaces the completion's span with the continuation text plus a
		/// trailing space and parks the caret after it. Returns <see langword="null"/> when there is nothing to accept.
		/// </summary>
		public InputCompletionAccept? Accept()
		{
			if (SelectedItem is not { } item || Result is not { } result)
				return null;

			var start = Math.Clamp(result.Span.Start, 0, _text.Length);
			var end = Math.Clamp(result.Span.End, start, _text.Length);
			var replacement = item.InsertText + " ";

			return new InputCompletionAccept(_text[..start] + replacement + _text[end..], start + replacement.Length);
		}

		private void Move(int delta) => SelectedIndex += delta;

		private void SetResult(InputCompletionResult? result)
		{
			Result = result;
			Items = result?.Items ?? [];
			State = result?.State;
			_selectedIndex = Items.Count > 0 ? Math.Clamp(result!.SelectedIndex, 0, Items.Count - 1) : 0;

			IsOpen = result is not null;

			RaisePropertyChanged(nameof(Result));
			RaisePropertyChanged(nameof(Items));
			RaisePropertyChanged(nameof(SelectedIndex));
			RaisePropertyChanged(nameof(SelectedItem));
			RaisePropertyChanged(nameof(State));
			RaisePropertyChanged(nameof(IsStateOnly));
			RaisePropertyChanged(nameof(CanAccept));
		}
	}
}
