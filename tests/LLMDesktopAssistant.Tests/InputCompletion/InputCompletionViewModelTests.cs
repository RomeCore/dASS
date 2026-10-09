using System;
using System.Linq;
using Avalonia.Input;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.LLM.MVVM;

namespace LLMDesktopAssistant.Tests.InputCompletion
{
	/// <summary>
	/// The completion view model: the open/close state, the selection, the keys it owns and the accept action.
	/// </summary>
	public class InputCompletionViewModelTests
	{
		/// <summary>
		/// A completion session with a fixed result: the view model only mirrors what the session holds and forwards the
		/// text, the caret and the close.
		/// </summary>
		private sealed class FakeService(InputCompletionResult? result) : IInputCompletionService
		{
			public event EventHandler? ResultChanged;

			public InputCompletionResult? Result { get; private set; } = result;

			public string? Text { get; private set; }
			public int CaretIndex { get; private set; }
			public int Closes { get; private set; }

			public void Update(string? text, int caretIndex)
			{
				Text = text;
				CaretIndex = caretIndex;
			}

			public void Close()
			{
				Closes++;
				if (Result is null)
					return;

				Result = null;
				ResultChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private static InputCompletionResult Result(InputCompletionSpan span, params string[] insertTexts) => new()
		{
			Span = span,
			State = new InputCompletionState { Kind = InputCompletionKind.Command },
			Items = [.. insertTexts.Select(text => new InputCompletionItem { InsertText = text })]
		};

		private static InputCompletionViewModel Vm(InputCompletionResult? result) => new(new FakeService(result));

		[Fact]
		public void Update_WithNoCompletion_KeepsThePopupClosed()
		{
			var vm = Vm(null);

			vm.Update("/zzz", 4);

			Assert.False(vm.IsOpen);
			Assert.Null(vm.Result);
		}

		[Fact]
		public void Update_WithACompletion_OpensThePopup()
		{
			var vm = Vm(Result(new InputCompletionSpan(0, 3), "/skill:grilling", "/agent:grilling"));

			vm.Update("/gr", 3);

			Assert.True(vm.IsOpen);
			Assert.Equal(2, vm.Items.Count);
			Assert.NotNull(vm.State);
			Assert.True(vm.CanAccept);
		}

		[Fact]
		public void Update_AStateOnlyResult_IsOpenWithoutItems()
		{
			var vm = Vm(new InputCompletionResult
			{
				Span = new InputCompletionSpan(0, 3),
				State = new InputCompletionState { Kind = InputCompletionKind.Command }
			});

			vm.Update("/zzz", 4);

			Assert.True(vm.IsOpen);
			Assert.True(vm.IsStateOnly);
			Assert.True(vm.ShowNoMatches);
			Assert.False(vm.CanAccept);
		}

		[Fact]
		public void Update_AnArgumentStateWithoutItems_DoesNotReadAsNoMatches()
		{
			var vm = Vm(new InputCompletionResult
			{
				Span = new InputCompletionSpan(0, 3),
				State = new InputCompletionState { Kind = InputCompletionKind.Argument }
			});

			vm.Update("/agent:x ", 9);

			Assert.True(vm.IsOpen);
			Assert.False(vm.ShowNoMatches);
		}

		[Fact]
		public void Rows_MirrorTheItems_AndFollowTheSelection()
		{
			var vm = Vm(Result(new InputCompletionSpan(0, 3), "a", "b"));
			vm.Update("/gr", 3);

			Assert.Equal(2, vm.Rows.Count);
			Assert.Same(vm.Items[0], vm.Rows[0].Item);
			Assert.True(vm.Rows[0].IsSelected);
			Assert.False(vm.Rows[1].IsSelected);
			Assert.False(vm.Icon.IsNone);

			vm.SelectedIndex = 1;

			Assert.False(vm.Rows[0].IsSelected);
			Assert.True(vm.Rows[1].IsSelected);
		}

		[Theory]
		[InlineData(Key.Down, 1)]
		[InlineData(Key.Up, 2)]
		public void TryHandleKey_MovesTheSelection_AndWraps(Key key, int expected)
		{
			var vm = Vm(Result(new InputCompletionSpan(0, 3), "a", "b", "c"));
			vm.Update("/gr", 3);

			Assert.True(vm.TryHandleKey(key));

			Assert.Equal(expected, vm.SelectedIndex);
		}

		[Fact]
		public void Update_ForwardsTheTextAndCaretToTheSession()
		{
			var service = new FakeService(null);
			var vm = new InputCompletionViewModel(service);

			vm.Update("/gr", 3);

			Assert.Equal("/gr", service.Text);
			Assert.Equal(3, service.CaretIndex);
		}

		[Fact]
		public void TryHandleKey_Escape_ClosesTheSessionToo()
		{
			// The inline preview is a projection of the session's result, so closing the popup must close that as well.
			var service = new FakeService(Result(new InputCompletionSpan(0, 3), "a"));
			var vm = new InputCompletionViewModel(service);
			vm.Update("/gr", 3);

			Assert.True(vm.TryHandleKey(Key.Escape));

			Assert.Equal(1, service.Closes);
			Assert.False(vm.IsOpen);
		}

		[Fact]
		public void TryHandleKey_Escape_Closes()
		{
			var vm = Vm(Result(new InputCompletionSpan(0, 3), "a"));
			vm.Update("/gr", 3);

			Assert.True(vm.TryHandleKey(Key.Escape));
			Assert.False(vm.IsOpen);
		}

		[Fact]
		public void TryHandleKey_WhenClosed_ReturnsFalse()
		{
			var vm = Vm(null);
			vm.Update("hello", 5);

			Assert.False(vm.TryHandleKey(Key.Down));
		}

		[Fact]
		public void Accept_ReplacesTheSpan_AndAppendsATrailingSpace()
		{
			var vm = Vm(Result(new InputCompletionSpan(0, 3), "/skill:grilling"));
			vm.Update("/gr", 3);

			var accepted = vm.Accept();

			Assert.NotNull(accepted);
			Assert.Equal("/skill:grilling ", accepted!.Value.Text);
			Assert.Equal(16, accepted.Value.Caret);
		}

		[Fact]
		public void Accept_UsesTheSelectedItem()
		{
			var vm = Vm(Result(new InputCompletionSpan(0, 3), "/skill:grilling", "/agent:grilling"));
			vm.Update("/gr", 3);
			vm.SelectedIndex = 1;

			var accepted = vm.Accept();

			Assert.Equal("/agent:grilling ", accepted!.Value.Text);
		}

		[Fact]
		public void Accept_WithNoItems_ReturnsNull()
		{
			var vm = Vm(new InputCompletionResult { Span = new InputCompletionSpan(0, 3) });
			vm.Update("/gr", 3);

			Assert.Null(vm.Accept());
		}
	}
}
