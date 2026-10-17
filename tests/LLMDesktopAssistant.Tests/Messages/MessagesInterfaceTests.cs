using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using RCLargeLanguageModels.Tasks;

namespace LLMDesktopAssistant.Tests.Messages;

/// <summary>
/// Tests for the <see cref="MessagesInterface"/> message grouping logic.
/// </summary>
public class MessagesInterfaceTests
{
	private static BranchedMessage CreateUserMessage(string content, int index) => new()
	{
		Message = new UserMessage
		{
			Content = content,
			CreatedAt = DateTime.UtcNow,
			SenderLogin = "user",
			Visibility = MessageVisibility.Always,
			VisibleTo = [],
			IsVisibleToWhiteList = false
		},
		MessageId = index,
		MessageIndex = index
	};

	private static BranchedMessage CreateAssistantMessage(string content, int index) => new()
	{
		Message = new AssistantMessage
		{
			Content = content,
			CreatedAt = DateTime.UtcNow,
			SenderAgentId = Guid.NewGuid(),
			AgentStageId = Guid.NewGuid()
		},
		MessageId = index,
		MessageIndex = index
	};

	private static BranchedMessage CreateUserLikeAssistantMessage(string content, int index) => new()
	{
		Message = new AssistantMessage
		{
			Content = content,
			CreatedAt = DateTime.UtcNow,
			SenderAgentId = Guid.NewGuid(),
			AgentStageId = Guid.NewGuid(),
			IsUserLike = true
		},
		MessageId = index,
		MessageIndex = index
	};

	private static string[] Contents(List<BranchedMessage> round) => round.Select(b => b.Message.Content).ToArray();

	[Fact]
	public void GroupMessagesIntoRounds_EmptyMessages_ReturnsNoRounds()
	{
		var rounds = MessagesInterface.GroupMessagesIntoRounds([]);

		Assert.Empty(rounds);
	}

	[Fact]
	public void GroupMessagesIntoRounds_SingleUserMessage_ReturnsSingleRound()
	{
		var messages = new[] { CreateUserMessage("u1", 0) };

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["u1"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_SingleAssistantMessage_ReturnsSingleRound()
	{
		var messages = new[] { CreateAssistantMessage("a1", 0) };

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["a1"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_UserThenAssistant_ReturnsSingleRound()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["u1", "a1"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_UserAssistantUserAssistant_ReturnsTwoRounds()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateUserMessage("u2", 2),
			CreateAssistantMessage("a2", 3)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		Assert.Equal(2, rounds.Count);
		Assert.Equal(["u1", "a1"], Contents(rounds[0]));
		Assert.Equal(["u2", "a2"], Contents(rounds[1]));
	}

	[Fact]
	public void GroupMessagesIntoRounds_ConsecutiveUserMessages_GroupedIntoSameRound()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateUserMessage("u2", 1),
			CreateAssistantMessage("a1", 2)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["u1", "u2", "a1"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_ConsecutiveAssistantMessages_GroupedIntoSameRound()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateAssistantMessage("a2", 2)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["u1", "a1", "a2"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_OnlyUserMessages_GroupedIntoSingleRound()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateUserMessage("u2", 1)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["u1", "u2"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_OnlyAssistantMessages_GroupedIntoSingleRound()
	{
		var messages = new[]
		{
			CreateAssistantMessage("a1", 0),
			CreateAssistantMessage("a2", 1)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["a1", "a2"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_MixedSequence_GroupsCorrectly()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateAssistantMessage("a2", 2),
			CreateUserMessage("u2", 3),
			CreateUserMessage("u3", 4),
			CreateAssistantMessage("a3", 5)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		Assert.Equal(2, rounds.Count);
		Assert.Equal(["u1", "a1", "a2"], Contents(rounds[0]));
		Assert.Equal(["u2", "u3", "a3"], Contents(rounds[1]));
	}

	[Fact]
	public void GroupMessagesIntoRounds_MaxLastRoundsZero_ReturnsAllRounds()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateUserMessage("u2", 2),
			CreateAssistantMessage("a2", 3),
			CreateUserMessage("u3", 4),
			CreateAssistantMessage("a3", 5)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages, maxLastRounds: 0);

		Assert.Equal(3, rounds.Count);
	}

	[Fact]
	public void GroupMessagesIntoRounds_MaxLastRoundsLessThanCount_ReturnsLastRounds()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateUserMessage("u2", 2),
			CreateAssistantMessage("a2", 3),
			CreateUserMessage("u3", 4),
			CreateAssistantMessage("a3", 5)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages, maxLastRounds: 2);

		Assert.Equal(2, rounds.Count);
		Assert.Equal(["u2", "a2"], Contents(rounds[0]));
		Assert.Equal(["u3", "a3"], Contents(rounds[1]));
	}

	[Fact]
	public void GroupMessagesIntoRounds_MaxLastRoundsEqualToCount_ReturnsAllRounds()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateUserMessage("u2", 2),
			CreateAssistantMessage("a2", 3)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages, maxLastRounds: 2);

		Assert.Equal(2, rounds.Count);
	}

	[Fact]
	public void GroupMessagesIntoRounds_MaxLastRoundsGreaterThanCount_ReturnsAllRounds()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages, maxLastRounds: 5);

		var round = Assert.Single(rounds);
		Assert.Equal(["u1", "a1"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_MaxLastRoundsEmptyMessages_ReturnsNoRounds()
	{
		var rounds = MessagesInterface.GroupMessagesIntoRounds([], maxLastRounds: 3);

		Assert.Empty(rounds);
	}

	[Fact]
	public void GroupMessagesIntoRounds_UserLikeAssistantAfterAssistant_StartsNewRound()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateUserLikeAssistantMessage("ul1", 2),
			CreateAssistantMessage("a2", 3)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		Assert.Equal(2, rounds.Count);
		Assert.Equal(["u1", "a1"], Contents(rounds[0]));
		Assert.Equal(["ul1", "a2"], Contents(rounds[1]));
	}

	[Fact]
	public void GroupMessagesIntoRounds_UserLikeAssistantAfterUser_JoinsSameRound()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateUserLikeAssistantMessage("ul1", 1),
			CreateAssistantMessage("a1", 2)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["u1", "ul1", "a1"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_ConsecutiveUserLikeAssistants_GroupedIntoSameRound()
	{
		var messages = new[]
		{
			CreateUserLikeAssistantMessage("ul1", 0),
			CreateUserLikeAssistantMessage("ul2", 1),
			CreateAssistantMessage("a1", 2)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages);

		var round = Assert.Single(rounds);
		Assert.Equal(["ul1", "ul2", "a1"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_UserLikeAssistantWithTreatUserLikeDisabled_TreatedAsAssistant()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateUserLikeAssistantMessage("ul1", 2),
			CreateAssistantMessage("a2", 3)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages, treatUserLikeAsUser: false);

		var round = Assert.Single(rounds);
		Assert.Equal(["u1", "a1", "ul1", "a2"], Contents(round));
	}

	[Fact]
	public void GroupMessagesIntoRounds_UserLikeAssistantWithMaxLastRounds_TakesLastRound()
	{
		var messages = new[]
		{
			CreateUserMessage("u1", 0),
			CreateAssistantMessage("a1", 1),
			CreateUserLikeAssistantMessage("ul1", 2),
			CreateAssistantMessage("a2", 3)
		};

		var rounds = MessagesInterface.GroupMessagesIntoRounds(messages, maxLastRounds: 1);

		var round = Assert.Single(rounds);
		Assert.Equal(["ul1", "a2"], Contents(round));
	}

	[Fact]
	public void IsEffectiveUserMessage_RealUserMessage_ReturnsTrue()
	{
		var message = CreateUserMessage("u1", 0);

		Assert.True(MessagesInterface.IsEffectiveUserMessage(message));
		Assert.True(MessagesInterface.IsEffectiveUserMessage(message, treatUserLikeAsUser: false));
	}

	[Fact]
	public void IsEffectiveUserMessage_UserLikeAssistant_ReturnsTrue()
	{
		var message = CreateUserLikeAssistantMessage("ul1", 0);

		Assert.True(MessagesInterface.IsEffectiveUserMessage(message));
		Assert.False(MessagesInterface.IsEffectiveUserMessage(message, treatUserLikeAsUser: false));
	}

	[Fact]
	public void IsEffectiveUserMessage_RegularAssistant_ReturnsFalse()
	{
		var message = CreateAssistantMessage("a1", 0);

		Assert.False(MessagesInterface.IsEffectiveUserMessage(message));
	}
}
