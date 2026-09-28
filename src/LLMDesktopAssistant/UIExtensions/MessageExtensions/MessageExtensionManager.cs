using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// The manager of message extensions. Used for instancing extensions for specific messages.
	/// </summary>
	public static class MessageExtensionManager
	{
		static readonly ImmutableList<Type> _userMessageExtensionTypes;
		static readonly ImmutableList<Type> _assistantMessageExtensionTypes;

		static MessageExtensionManager()
		{
			var extensions = ReflectionUtility.GetTypesWithAttribute<MessageExtension, MessageExtensionAttribute>()
				.ToList();

			_userMessageExtensionTypes = extensions
				.Where(e => e.Attribute.Targets.HasFlag(MessageExtensionTargets.User))
				.Select(e => e.Type)
				.ToImmutableList();

			_assistantMessageExtensionTypes = extensions
				.Where(e => e.Attribute.Targets.HasFlag(MessageExtensionTargets.Assistant))
				.Select(e => e.Type)
				.ToImmutableList();
		}

		/// <summary>
		/// Instantiates extensions for the given message.
		/// </summary>
		/// <param name="messageVm">The view model of the message.</param>
		/// <param name="chat">The chat the message belongs to.</param>
		/// <returns>The list of extensions.</returns>
		/// <exception cref="InvalidCastException">Thrown if view model type is not compatible to message type.</exception>
		/// <exception cref="ArgumentException">Thrown if the message type is not supported.</exception>
		public static ImmutableList<MessageExtension> CreateExtensions(MessageViewModelBase messageVm, Chat chat)
		{
			switch (messageVm)
			{
				case UserMessageViewModel:

					return _userMessageExtensionTypes
						.Instantiate<MessageExtension>(chat.Services, messageVm)
						.OrderBy(ext => ext.Order)
						.ToImmutableList();

				case AssistantMessageViewModel:

					return _assistantMessageExtensionTypes
						.Instantiate<MessageExtension>(chat.Services, messageVm)
						.OrderBy(ext => ext.Order)
						.ToImmutableList();

				default:

					throw new ArgumentException($"Unexpected message view model type: {messageVm.GetType()}");
			}
		}
	}
}