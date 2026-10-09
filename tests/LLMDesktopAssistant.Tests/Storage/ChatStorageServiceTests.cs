using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Storage;
using LLMDesktopAssistant.Settings;
using RCLargeLanguageModels.Tasks;

namespace LLMDesktopAssistant.Tests.Storage;

/// <summary>
/// Tests for <see cref="ChatStorageService"/> tree operations: node/pointer bookkeeping,
/// message collection updates, and guaranteed rollback (without poisoning the storage
/// or the cached model) when a transaction fails.
/// </summary>
[Collection("Storage")]
public class ChatStorageServiceTests
{
	private static ChatStorageTestContext CreateContext() => new();

	private static ChatStorageTestContext CreateContextWithFaults(out FaultInjectingStorageLock faultLock)
	{
		FaultInjectingStorageLock? fault = null;
		var context = new ChatStorageTestContext(config =>
			fault = new FaultInjectingStorageLock(new ChatStorageLock(config)));
		faultLock = fault!;
		return context;
	}

	// ========== AppendMessage ==========

	[Fact]
	public void AppendMessage_FirstMessage_CreatesRootNode()
	{
		using var ctx = CreateContext();
		var message = ChatStorageTestContext.CreateMessage("one");

		ctx.Service.AppendMessage(message);

		Assert.Single(ctx.Chat.Messages);
		Assert.Same(message, ctx.Chat.Messages[0].Message);
		Assert.Equal(0, ctx.Chat.Messages[0].MessageIndex);

		var node = Assert.Single(ctx.Nodes);
		Assert.True(node.IsRootNode);
		Assert.Equal(ctx.Config.ChatId, node.ParentId);
		Assert.Equal(-1, node.SelectedNodeId);

		var stored = Assert.Single(ctx.Messages);
		Assert.Equal("one", stored.Content);
		Assert.Equal(RoleModel.User, stored.Role);

		Assert.Equal(node.Id, ctx.Model.RootNodeId);
		Assert.Equal(node.Id, ctx.Model.LeafNodeId);
	}

	[Fact]
	public void AppendMessage_SecondMessage_ChainsChildNodeAndSelectsIt()
	{
		using var ctx = CreateContext();

		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal(2, ctx.Messages.Count);

		var root = ctx.RootNode;
		var child = ctx.Nodes.Single(n => !n.IsRootNode);
		Assert.Equal(root.Id, child.ParentId);
		Assert.Equal(child.Id, ctx.Nodes.Single(n => n.Id == root.Id).SelectedNodeId);
		Assert.Equal(root.Id, ctx.Model.RootNodeId);
		Assert.Equal(child.Id, ctx.Model.LeafNodeId);
	}

	// ========== EditMessage ==========

	[Fact]
	public void EditMessage_OnChild_CreatesSiblingBranchAndKeepsOldOne()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));

		var root = ctx.RootNode;
		var originalChild = ctx.Nodes.Single(n => !n.IsRootNode);

		var edited = ChatStorageTestContext.CreateMessage("two-edited");
		ctx.Service.EditMessage(1, edited);

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Same(edited, ctx.Chat.Messages[1].Message);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);

		// the old branch stays in the database
		Assert.Equal(3, ctx.Messages.Count);
		Assert.Contains(ctx.Nodes, n => n.Id == originalChild.Id);

		var editedNode = ctx.NodeByMessageId(ctx.Chat.Messages[1].MessageId);
		Assert.False(editedNode.IsRootNode);
		Assert.Equal(root.Id, editedNode.ParentId);
		Assert.Equal(editedNode.Id, ctx.Nodes.Single(n => n.Id == root.Id).SelectedNodeId);
		Assert.Equal(editedNode.Id, ctx.Model.LeafNodeId);
		Assert.Equal(root.Id, ctx.Model.RootNodeId);
	}

	[Fact]
	public void EditMessage_OnRoot_ReplacesRootPointer()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));

		var oldRoot = ctx.RootNode;
		var edited = ChatStorageTestContext.CreateMessage("one-edited");
		ctx.Service.EditMessage(0, edited);

		Assert.Same(edited, ctx.Chat.Messages[0].Message);
		Assert.Equal(2, ctx.Messages.Count);

		var newRoot = ctx.RootNode;
		Assert.NotEqual(oldRoot.Id, newRoot.Id);
		Assert.Equal(ctx.Config.ChatId, newRoot.ParentId);
		Assert.Equal(-1, newRoot.SelectedNodeId);
		Assert.Equal(newRoot.Id, ctx.Model.RootNodeId);
		Assert.Equal(newRoot.Id, ctx.Model.LeafNodeId);
		Assert.Contains(ctx.Nodes, n => n.Id == oldRoot.Id);
	}

	// ========== SwitchBranch ==========

	[Fact]
	public void SwitchBranch_SwitchesBetweenBranches_AndReloadsMessages()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.EditMessage(1, ChatStorageTestContext.CreateMessage("two-edited"));

		Assert.Equal("two-edited", ctx.Chat.Messages[1].Message.Content);

		var root = ctx.RootNode;
		var oldBranch = ctx.Nodes.Where(n => !n.IsRootNode).OrderBy(n => n.Id).First();

		ctx.Service.SwitchBranch(1, 0);

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal(oldBranch.Id, ctx.Nodes.Single(n => n.Id == root.Id).SelectedNodeId);
		Assert.Equal(oldBranch.Id, ctx.Model.LeafNodeId);
		Assert.Equal(3, ctx.Messages.Count);

		// switch back to the edited branch
		ctx.Service.SwitchBranch(1, 1);

		Assert.Equal("two-edited", ctx.Chat.Messages[1].Message.Content);
		var editedNode = ctx.NodeByMessageId(ctx.Chat.Messages[1].MessageId);
		Assert.Equal(editedNode.Id, ctx.Model.LeafNodeId);
	}

	[Fact]
	public void SwitchBranch_InvalidIndex_Throws_AndStorageStaysUsable()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.EditMessage(1, ChatStorageTestContext.CreateMessage("two-edited"));

		var leafBefore = ctx.Model.LeafNodeId;
		var rootBefore = ctx.Model.RootNodeId;

		Assert.Throws<ArgumentOutOfRangeException>(() => ctx.Service.SwitchBranch(1, 42));
		Assert.Throws<ArgumentOutOfRangeException>(() => ctx.Service.SwitchBranch(1, -1));

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("two-edited", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal(leafBefore, ctx.Model.LeafNodeId);
		Assert.Equal(rootBefore, ctx.Model.RootNodeId);
		Assert.Equal(3, ctx.Nodes.Count);

		// Regression: a rejected call must never leave an open transaction behind.
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("three"));

		Assert.Equal(3, ctx.Chat.Messages.Count);
		Assert.Equal("three", ctx.Chat.Messages[2].Message.Content);
		Assert.Equal(4, ctx.Messages.Count);
		Assert.Equal(4, ctx.Nodes.Count);
	}

	// ========== PlaceNewBranch ==========

	[Fact]
	public void PlaceNewBranch_TruncatesTail_AndResetsLeaf()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("three"));

		ctx.Service.PlaceNewBranch(1);

		Assert.Single(ctx.Chat.Messages);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);

		var root = ctx.RootNode;
		Assert.Equal(-1, root.SelectedNodeId);
		Assert.Equal(root.Id, ctx.Model.LeafNodeId);
		Assert.Equal(root.Id, ctx.Model.RootNodeId);

		// the detached tail stays in the database
		Assert.Equal(3, ctx.Messages.Count);
		Assert.Equal(3, ctx.Nodes.Count);
	}

	[Fact]
	public void PlaceNewBranch_OnFirstMessage_EmptiesChat()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));

		ctx.Service.PlaceNewBranch(0);

		Assert.Empty(ctx.Chat.Messages);
		Assert.Equal(-1, ctx.Model.RootNodeId);
		Assert.Equal(-1, ctx.Model.LeafNodeId);
		Assert.Equal(2, ctx.Messages.Count);
	}

	// ========== DeleteMessageWithDescendants ==========

	[Fact]
	public void DeleteMessageWithDescendants_DeletesSelectedBranch_AndSelectsSibling()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.EditMessage(1, ChatStorageTestContext.CreateMessage("two-edited"));

		ctx.Service.DeleteMessageWithDescendants(1);

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);

		var root = ctx.RootNode;
		var survivor = ctx.Nodes.Single(n => !n.IsRootNode);
		Assert.Equal(survivor.Id, root.SelectedNodeId);
		Assert.Equal(survivor.Id, ctx.Model.LeafNodeId);

		// the deleted branch is gone from the database
		Assert.Equal(2, ctx.Nodes.Count);
		Assert.Equal(2, ctx.Messages.Count);
	}

	[Fact]
	public void DeleteMessageWithDescendants_OnRoot_EmptiesChat()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));

		ctx.Service.DeleteMessageWithDescendants(0);

		Assert.Empty(ctx.Chat.Messages);
		Assert.Equal(-1, ctx.Model.RootNodeId);
		Assert.Equal(-1, ctx.Model.LeafNodeId);
		Assert.Empty(ctx.Nodes);
		Assert.Empty(ctx.Messages);
	}

	[Fact]
	public void DeleteMessageWithDescendants_RemovesAllChildBranches()
	{
		using var ctx = CreateContext();
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("three"));
		ctx.Service.EditMessage(2, ChatStorageTestContext.CreateMessage("three-edited"));

		ctx.Service.DeleteMessageWithDescendants(1);

		Assert.Single(ctx.Chat.Messages);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);

		var root = ctx.RootNode;
		Assert.Single(ctx.Nodes);
		Assert.Equal(root.Id, ctx.Model.LeafNodeId);
		Assert.Equal(root.Id, ctx.Model.RootNodeId);
		Assert.Single(ctx.Messages);
	}

	[Fact]
	public void DeleteMessageWithDescendants_CascadesToToolCalls()
	{
		using var ctx = CreateContext();
		var message = ChatStorageTestContext.CreateMessage("one");
		message.ToolCalls.Add(new ToolCall
		{
			ToolName = "test-tool",
			ToolCallId = "call-1",
			CompletionToken = CompletionToken.Success
		});

		ctx.Service.AppendMessage(message);

		Assert.Equal(1, ctx.Database.ToolCalls.Count());

		ctx.Service.DeleteMessageWithDescendants(0);

		Assert.Empty(ctx.Chat.Messages);
		Assert.Empty(ctx.Nodes);
		Assert.Empty(ctx.Messages);
		Assert.Equal(0, ctx.Database.ToolCalls.Count());
	}

	[Fact]
	public void DeleteMessageWithDescendants_Simple_ReloadsCorrectlyAfter()
	{
		using var ctx = CreateContext();

		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("three"));

		ctx.Service.DeleteMessageWithDescendants(2);

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);

		ctx.Service.Reload();

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
	}

	[Fact]
	public void DeleteMessageWithDescendants_MultipleFromTail_ReloadsCorrectlyAfter()
	{
		using var ctx = CreateContext();

		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("three"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("four"));

		ctx.Service.DeleteMessageWithDescendants(3);
		ctx.Service.DeleteMessageWithDescendants(2);

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal(2, ctx.Nodes.Count);
		Assert.Equal(2, ctx.Messages.Count);

		var twoNode = ctx.NodeByMessageId(ctx.Chat.Messages[1].MessageId);
		Assert.Equal(-1, twoNode.SelectedNodeId);
		Assert.Equal(twoNode.Id, ctx.Model.LeafNodeId);

		ctx.Service.Reload();

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
	}

	[Fact]
	public void DeleteMessageWithDescendants_WithBranches_ReloadsCorrectlyAfter_Var1()
	{
		using var ctx = CreateContext();

		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("three-1"));
		ctx.Service.EditMessage(2, ChatStorageTestContext.CreateMessage("three-2"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("four"));

		ctx.Service.DeleteMessageWithDescendants(2);

		Assert.Equal(3, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal("three-1", ctx.Chat.Messages[2].Message.Content);

		ctx.Service.Reload();

		Assert.Equal(3, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal("three-1", ctx.Chat.Messages[2].Message.Content);
	}

	[Fact]
	public void DeleteMessageWithDescendants_WithBranches_ReloadsCorrectlyAfter_Var2()
	{
		using var ctx = CreateContext();

		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("three-1"));
		ctx.Service.EditMessage(2, ChatStorageTestContext.CreateMessage("three-2"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("four"));
		ctx.Service.SwitchBranch(2, 0);

		ctx.Service.DeleteMessageWithDescendants(2);

		Assert.Equal(4, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal("three-2", ctx.Chat.Messages[2].Message.Content);
		Assert.Equal("four", ctx.Chat.Messages[3].Message.Content);

		ctx.Service.Reload();

		Assert.Equal(4, ctx.Chat.Messages.Count);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal("three-2", ctx.Chat.Messages[2].Message.Content);
		Assert.Equal("four", ctx.Chat.Messages[3].Message.Content);
	}

	// ========== Transaction failures ==========

	[Fact]
	public void AppendMessage_TransactionFailure_RollsBackAndKeepsStorageUsable()
	{
		using var ctx = CreateContextWithFaults(out var fault);
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));

		var leafBefore = ctx.Model.LeafNodeId;

		fault.FailAfterAction = true;
		var second = ChatStorageTestContext.CreateMessage("two");
		var exception = Assert.Throws<InvalidOperationException>(() => ctx.Service.AppendMessage(second));
		Assert.Equal("Injected failure", exception.Message);

		// fully rolled back
		Assert.Single(ctx.Chat.Messages);
		Assert.Single(ctx.Nodes);
		Assert.Single(ctx.Messages);
		Assert.Equal(leafBefore, ctx.Model.LeafNodeId);

		// the storage is not poisoned: the same message can be appended again
		fault.FailAfterAction = false;
		ctx.Service.AppendMessage(second);

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Same(second, ctx.Chat.Messages[1].Message);
		Assert.Equal(2, ctx.Nodes.Count);
		Assert.Equal(2, ctx.Messages.Count);

		// the fresh synchronizer persists subsequent changes
		second.Content = "two-updated";
		Assert.Equal("two-updated", ctx.Messages.Single(m => m.Id == ctx.Chat.Messages[1].MessageId).Content);
	}

	[Fact]
	public void EditMessage_TransactionFailure_KeepsOriginalBranch()
	{
		using var ctx = CreateContextWithFaults(out var fault);
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));

		var leafBefore = ctx.Model.LeafNodeId;

		fault.FailAfterAction = true;
		var edited = ChatStorageTestContext.CreateMessage("two-edited");
		Assert.Throws<InvalidOperationException>(() => ctx.Service.EditMessage(1, edited));

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal(2, ctx.Messages.Count);
		Assert.Equal(leafBefore, ctx.Model.LeafNodeId);

		fault.FailAfterAction = false;
		ctx.Service.EditMessage(1, edited);

		Assert.Equal("two-edited", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal(3, ctx.Messages.Count);
	}

	[Fact]
	public void SwitchBranch_TransactionFailure_KeepsCurrentBranch()
	{
		using var ctx = CreateContextWithFaults(out var fault);
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.EditMessage(1, ChatStorageTestContext.CreateMessage("two-edited"));

		var leafBefore = ctx.Model.LeafNodeId;
		var root = ctx.RootNode;

		fault.FailAfterAction = true;
		Assert.Throws<InvalidOperationException>(() => ctx.Service.SwitchBranch(1, 0));

		Assert.Equal(2, ctx.Chat.Messages.Count);
		Assert.Equal("two-edited", ctx.Chat.Messages[1].Message.Content);
		Assert.Equal(leafBefore, ctx.Model.LeafNodeId);
		Assert.Equal(leafBefore, ctx.Nodes.Single(n => n.Id == root.Id).SelectedNodeId);

		fault.FailAfterAction = false;
		ctx.Service.SwitchBranch(1, 0);

		Assert.Equal("two", ctx.Chat.Messages[1].Message.Content);
	}

	[Fact]
	public void DeleteMessageWithDescendants_TransactionFailure_KeepsEverything()
	{
		using var ctx = CreateContextWithFaults(out var fault);
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("one"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("two"));
		ctx.Service.AppendMessage(ChatStorageTestContext.CreateMessage("three"));

		var leafBefore = ctx.Model.LeafNodeId;

		fault.FailAfterAction = true;
		Assert.Throws<InvalidOperationException>(() => ctx.Service.DeleteMessageWithDescendants(1));

		Assert.Equal(3, ctx.Chat.Messages.Count);
		Assert.Equal(3, ctx.Nodes.Count);
		Assert.Equal(3, ctx.Messages.Count);
		Assert.Equal(leafBefore, ctx.Model.LeafNodeId);

		fault.FailAfterAction = false;
		ctx.Service.DeleteMessageWithDescendants(1);

		Assert.Single(ctx.Chat.Messages);
		Assert.Equal("one", ctx.Chat.Messages[0].Message.Content);
		Assert.Single(ctx.Messages);
	}
}
