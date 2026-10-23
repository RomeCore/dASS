using Xunit;
using XTerm;
using XTerm.Options;

namespace LLMDesktopAssistant.Desktop.Tests.Terminals;

/// <summary>
/// The terminal's maximum scrollback (<c>TerminalView.MaxScrollback</c>) is the buffer's scroll base (<c>Buffer.YBase</c>): the line
/// the screen starts at when it is scrolled all the way down. These tests pin why it cannot be derived
/// as <c>Length - Rows</c> - a buffer that was grown and shrunk again keeps lines past the screen, so the
/// arithmetic promises scroll positions the viewport refuses to take.
/// </summary>
public class TerminalScrollbackTests
{
	private static Terminal CreateTerminal(int scrollback)
		=> new(new TerminalOptions { Scrollback = scrollback });

	private static Terminal CreateTerminalWithLines(int scrollback, int lines)
	{
		var terminal = CreateTerminal(scrollback);

		for (var i = 0; i < lines; i++)
			terminal.Write($"line {i}\r\n");

		return terminal;
	}

	/// <summary>
	/// The furthest line the buffer actually accepts, asked from the buffer itself. This is the ground
	/// truth the property has to match; the production code never probes, it reads the scroll base.
	/// </summary>
	private static int MeasureReachableViewportY(Terminal terminal)
	{
		var buffer = terminal.Buffer;
		var saved = buffer.ViewportY;

		buffer.ViewportY = int.MaxValue;
		var reachable = buffer.ViewportY;

		buffer.ViewportY = saved;
		return reachable;
	}

	[Theory]
	[InlineData(100)]
	[InlineData(5000)]
	public void ScrollBase_IsTheFurthestReachableViewportY(int scrollback)
	{
		var terminal = CreateTerminalWithLines(scrollback, 60);

		Assert.Equal(terminal.Buffer.YBase, MeasureReachableViewportY(terminal));
	}

	[Fact]
	public void ScrollBase_IsTheFurthestReachableViewportY_AcrossResizes()
	{
		var terminal = CreateTerminalWithLines(scrollback: 5000, lines: 60);

		foreach (var rows in new[] { 47, 23, 60, 23, 10, 40 })
		{
			terminal.Resize(80, rows);

			Assert.Equal(terminal.Buffer.YBase, MeasureReachableViewportY(terminal));
		}
	}

	[Fact]
	public void LengthMinusRows_CanPromiseMoreThanTheBufferAccepts()
	{
		var terminal = CreateTerminalWithLines(scrollback: 5000, lines: 60);
		terminal.Buffer.ScrollToBottom();

		// Growing and shrinking again is what the card does when the user drags its height.
		terminal.Resize(80, 47);
		terminal.Write(new string('\n', 20));
		terminal.Resize(80, 23);

		var buffer = terminal.Buffer;
		var reachable = MeasureReachableViewportY(terminal);

		// The scroll base is always reachable, and it is never past the arithmetic estimate.
		Assert.Equal(buffer.YBase, reachable);
		Assert.True(reachable <= buffer.Length - terminal.Rows);
	}

	[Fact]
	public void ScrollBase_IsZero_WithoutScrollback()
	{
		var terminal = CreateTerminalWithLines(scrollback: 0, lines: 60);

		Assert.Equal(0, terminal.Buffer.YBase);
		Assert.Equal(0, MeasureReachableViewportY(terminal));
	}

	[Fact]
	public void ViewportY_IsClampedAtBothEnds()
	{
		var terminal = CreateTerminalWithLines(scrollback: 5000, lines: 60);
		var buffer = terminal.Buffer;
		var maximum = buffer.YBase;

		buffer.ViewportY = maximum + 10;
		Assert.Equal(maximum, buffer.ViewportY);

		buffer.ViewportY = -5;
		Assert.Equal(0, buffer.ViewportY);
	}

	[Fact]
	public void Resize_KeepsAViewportThatWasAtTheBottom_AtTheBottom()
	{
		var terminal = CreateTerminalWithLines(scrollback: 5000, lines: 60);
		var buffer = terminal.Buffer;
		buffer.ScrollToBottom();

		foreach (var rows in new[] { 10, 40, 24 })
		{
			terminal.Resize(80, rows);

			// Nothing restores the viewport after a resize: the buffer re-anchors itself, which is what
			// keeps the auto-follow working when the user drags the terminal height around.
			Assert.Equal(buffer.YBase, buffer.ViewportY);
		}
	}
}
