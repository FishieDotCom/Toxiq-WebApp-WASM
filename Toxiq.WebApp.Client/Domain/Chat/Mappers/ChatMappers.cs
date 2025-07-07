// Toxiq.WebApp.Client/Domain/Chat/Mappers/ChatMappers.cs
// DTO to Domain model mappers - clean separation of concerns

using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Domain.Chat.Models;

namespace Toxiq.WebApp.Client.Domain.Chat.Mappers
{
    /// <summary>
    /// Mappers to convert between DTOs and Domain models
    /// Keeps business logic separate from API contracts
    /// </summary>
    public static class ChatMappers
    {
        #region Conversation Mapping

        /// <summary>
        /// Convert DTO to Domain model
        /// </summary>
        public static ChatConversation ToDomain(this Conversation dto, Guid currentUserId)
        {
            var conversation = new ChatConversation
            {
                Id = dto.Id,
                ConversationName = dto.ConversationName ?? string.Empty,
                ChatStarted = dto.ChatStarted,
                IsGroup = dto.IsGroup,
                Participants = dto.Users?.Select(u => u.ToDomain(currentUserId)).ToList() ?? new()
            };

            return conversation;
        }

        /// <summary>
        /// Convert Domain model to DTO for API calls
        /// </summary>
        public static Conversation ToDto(this ChatConversation domain)
        {
            return new Conversation
            {
                Id = domain.Id,
                ConversationName = domain.ConversationName,
                ChatStarted = domain.ChatStarted,
                IsGroup = domain.IsGroup,
                Users = domain.Participants.Select(p => p.ToDto()).ToList()
            };
        }

        #endregion

        #region Participant Mapping

        /// <summary>
        /// Convert DTO to Domain model
        /// </summary>
        public static ChatParticipant ToDomain(this ConversationUser dto, Guid currentUserId)
        {
            return new ChatParticipant
            {
                Id = dto.Id,
                UserId = dto.UserId,
                Name = dto.Name ?? string.Empty,
                Username = string.Empty, // Not available in DTO
                JoinedDate = dto.JoinedDate,
                IsCurrentUser = dto.UserId == currentUserId,
                IsOnline = false, // Would need real-time data
                LastSeen = null // Would need real-time data
            };
        }

        /// <summary>
        /// Convert Domain model to DTO
        /// </summary>
        public static ConversationUser ToDto(this ChatParticipant domain)
        {
            return new ConversationUser
            {
                Id = domain.Id,
                UserId = domain.UserId,
                Name = domain.Name,
                JoinedDate = domain.JoinedDate
            };
        }

        #endregion

        #region Message Mapping

        /// <summary>
        /// Convert DTO to Domain model
        /// </summary>
        public static ChatMessage ToDomain(this Message dto, Guid currentUserId, ChatConversation? conversation = null)
        {
            var message = new ChatMessage
            {
                Id = dto.Id,
                SenderId = dto.SenderID,
                ConversationId = conversation?.Id ?? Guid.Empty,
                Date = dto.Date,
                ReplyToMessageId = dto.ReplyTo,
                Type = (ChatMessageType)(int)dto.Type,
                Content = dto.Content ?? string.Empty,
                IsSentByCurrentUser = dto.SenderID == currentUserId,
                IsGroupMessage = conversation?.IsGroup ?? false,
                SenderName = dto.SenderName ?? string.Empty,
                IsLastMessage = dto.IsLastMessage,
            };

            return message;
        }

        /// <summary>
        /// Convert MessageDto to Domain model
        /// </summary>
        public static ChatMessage ToDomain(this MessageDto dto, Guid currentUserId)
        {
            return new ChatMessage
            {
                Id = dto.Id,
                SenderId = dto.SenderID,
                Date = dto.Date,
                ReplyToMessageId = dto.ReplyTo,
                Type = (ChatMessageType)(int)dto.Type,
                Content = dto.Content ?? string.Empty,
                IsSentByCurrentUser = dto.SenderID == currentUserId
            };
        }

        /// <summary>
        /// Convert Domain model to DTO for API calls
        /// </summary>
        public static MessageDto ToDto(this ChatMessage domain)
        {
            return new MessageDto
            {
                Id = domain.Id,
                SenderID = domain.SenderId,
                RecipientID = Guid.Empty, // Set by API
                Date = domain.Date,
                ReplyTo = domain.ReplyToMessageId,
                Type = (MessageType)(int)domain.Type,
                Content = domain.Content
            };
        }

        /// <summary>
        /// Convert SendMessageRequest to DTO
        /// </summary>
        public static MessageDto ToDto(this SendMessageRequest request, Guid senderId)
        {
            return new MessageDto
            {
                Id = request.TemporaryId ?? Guid.NewGuid(),
                SenderID = senderId,
                RecipientID = Guid.Empty, // Will be set by API based on conversation
                Date = DateTime.UtcNow,
                ReplyTo = request.ReplyToMessageId,
                Type = (MessageType)(int)request.Type,
                Content = request.Content
            };
        }

        #endregion

        #region Message Response Mapping

        /// <summary>
        /// Convert DTO to Domain model
        /// </summary>
        public static ChatMessagesResult ToDomain(this MessageResponse dto, Guid currentUserId, ChatConversation? conversation = null)
        {
            return new ChatMessagesResult
            {
                Messages = dto.Messages?.Select(m => m.ToDomain(currentUserId)).ToList() ?? new(),
                TotalPages = dto.TotalPages,
                TotalCount = dto.TotalCount,
                CurrentPage = dto.CurrentPage,
                PageSize = dto.PageSize
            };
        }

        #endregion

        #region Batch Mapping Extensions

        /// <summary>
        /// Convert list of conversation DTOs to domain models
        /// </summary>
        public static List<ChatConversation> ToDomainList(this List<Conversation> dtos, Guid currentUserId)
        {
            return dtos?.Select(dto => dto.ToDomain(currentUserId)).ToList() ?? new();
        }

        /// <summary>
        /// Convert list of message DTOs to domain models
        /// </summary>
        public static List<ChatMessage> ToDomainList(this List<MessageDto> dtos, Guid currentUserId)
        {
            return dtos?.Select(dto => dto.ToDomain(currentUserId)).ToList() ?? new();
        }

        /// <summary>
        /// Convert list of domain conversations to DTOs
        /// </summary>
        public static List<Conversation> ToDtoList(this List<ChatConversation> domains)
        {
            return domains?.Select(domain => domain.ToDto()).ToList() ?? new();
        }

        /// <summary>
        /// Convert list of domain messages to DTOs
        /// </summary>
        public static List<MessageDto> ToDtoList(this List<ChatMessage> domains)
        {
            return domains?.Select(domain => domain.ToDto()).ToList() ?? new();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Create optimistic message for immediate UI feedback
        /// </summary>
        public static ChatMessage CreateOptimisticMessage(SendMessageRequest request, Guid senderId, string senderName)
        {
            return new ChatMessage
            {
                Id = request.TemporaryId ?? Guid.NewGuid(),
                SenderId = senderId,
                ConversationId = request.ConversationId,
                Date = DateTime.Now,
                ReplyToMessageId = request.ReplyToMessageId,
                Type = request.Type,
                Content = request.Content,
                IsSentByCurrentUser = true,
                SenderName = senderName,
                IsDelivered = false // Will be updated when API confirms
            };
        }

        /// <summary>
        /// Update optimistic message with server response
        /// </summary>
        public static void UpdateFromServerResponse(this ChatMessage optimisticMessage, Message serverResponse)
        {
            optimisticMessage.Id = serverResponse.Id;
            optimisticMessage.Date = serverResponse.Date;
            optimisticMessage.IsDelivered = true;
            optimisticMessage.IsFailed = false;
        }

        /// <summary>
        /// Create conversation from CreateConversationRequest
        /// </summary>
        public static ChatConversation CreateFromRequest(CreateConversationRequest request, Guid currentUserId)
        {
            return new ChatConversation
            {
                Id = Guid.NewGuid(), // Temporary until server assigns
                ConversationName = request.Name ?? string.Empty,
                ChatStarted = DateTime.Now,
                IsGroup = request.IsGroup,
                Participants = new()
                {
                    new ChatParticipant
                    {
                        UserId = currentUserId,
                        IsCurrentUser = true,
                        JoinedDate = DateTime.Now
                    }
                }
            };
        }

        #endregion
    }

    /// <summary>
    /// Extensions for working with chat domain models
    /// </summary>
    public static class ChatDomainExtensions
    {
        /// <summary>
        /// Add message to conversation and update last message
        /// </summary>
        public static void AddMessage(this ChatConversation conversation, ChatMessage message)
        {
            conversation.UpdateLastMessage(message);
        }

        /// <summary>
        /// Check if conversation contains user
        /// </summary>
        public static bool ContainsUser(this ChatConversation conversation, Guid userId)
        {
            return conversation.Participants.Any(p => p.UserId == userId);
        }

        /// <summary>
        /// Get conversation display info for current user
        /// </summary>
        public static (string displayName, string avatar) GetDisplayInfo(this ChatConversation conversation)
        {
            return (conversation.DisplayName, conversation.UserAvatar);
        }

        /// <summary>
        /// Sort conversations by last message time
        /// </summary>
        public static List<ChatConversation> SortByLastMessage(this List<ChatConversation> conversations)
        {
            return conversations
                .OrderByDescending(c => c.LastMessage?.Date ?? c.ChatStarted)
                .ToList();
        }

        /// <summary>
        /// Filter conversations by search term
        /// </summary>
        public static List<ChatConversation> FilterBySearchTerm(this List<ChatConversation> conversations, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return conversations;

            var term = searchTerm.ToLowerInvariant();
            return conversations
                .Where(c =>
                    c.DisplayName.ToLowerInvariant().Contains(term) ||
                    c.LastMessage?.Content.ToLowerInvariant().Contains(term) == true ||
                    c.Participants.Any(p => p.Name.ToLowerInvariant().Contains(term)))
                .ToList();
        }
    }
}