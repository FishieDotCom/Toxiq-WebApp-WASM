// Toxiq.WebApp.Client/Domain/Chat/Models/ChatConversation.cs
// Chat domain models - business logic layer separate from DTOs

using System.ComponentModel;

namespace Toxiq.WebApp.Client.Domain.Chat.Models
{
    /// <summary>
    /// Domain model for chat conversations - business logic representation
    /// Separate from DTOs to keep business logic clean
    /// </summary>
    public class ChatConversation : INotifyPropertyChanged
    {
        private string _conversationName = string.Empty;
        private bool _hasUnreadMessages;
        private ChatMessage? _lastMessage;
        private int _unreadCount;

        public Guid Id { get; set; }
        public string ConversationName
        {
            get => _conversationName;
            set
            {
                _conversationName = value;
                OnPropertyChanged(nameof(ConversationName));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public DateTime ChatStarted { get; set; }
        public List<ChatParticipant> Participants { get; set; } = new();
        public bool IsGroup { get; set; }
        public bool HasUnreadMessages
        {
            get => _hasUnreadMessages;
            set
            {
                _hasUnreadMessages = value;
                OnPropertyChanged(nameof(HasUnreadMessages));
            }
        }

        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                _unreadCount = value;
                OnPropertyChanged(nameof(UnreadCount));
                HasUnreadMessages = value > 0;
            }
        }

        public ChatMessage? LastMessage
        {
            get => _lastMessage;
            set
            {
                _lastMessage = value;
                OnPropertyChanged(nameof(LastMessage));
                OnPropertyChanged(nameof(LastMessagePreview));
                OnPropertyChanged(nameof(LastMessageTime));
            }
        }

        // Computed properties for UI
        public string DisplayName => IsGroup
            ? ConversationName
            : Participants.FirstOrDefault()?.Name ?? "Unknown User";

        public string UserAvatar => IsGroup
            ? GetGroupAvatar()
            : GetUserInitials(Participants.FirstOrDefault()?.Name);

        public string LastMessagePreview => LastMessage?.GetPreviewText() ?? "No messages yet";

        public string LastMessageTime => LastMessage?.Date.ToString("HH:mm") ?? "";

        public bool IsCurrentUserAlone => Participants.Count == 1;

        public ChatParticipant? OtherParticipant => IsGroup
            ? null
            : Participants.FirstOrDefault(p => !p.IsCurrentUser);

        // Methods
        public void AddParticipant(ChatParticipant participant)
        {
            if (!Participants.Any(p => p.UserId == participant.UserId))
            {
                Participants.Add(participant);
                OnPropertyChanged(nameof(Participants));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public void RemoveParticipant(Guid userId)
        {
            var participant = Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                Participants.Remove(participant);
                OnPropertyChanged(nameof(Participants));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public void UpdateLastMessage(ChatMessage message)
        {
            LastMessage = message;

            // Update unread count if message is not from current user
            if (!message.IsSentByCurrentUser)
            {
                UnreadCount++;
            }
        }

        public void MarkAsRead()
        {
            UnreadCount = 0;
            HasUnreadMessages = false;
        }

        private string GetUserInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "?";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return name.Substring(0, 1).ToUpper();

            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();

            return $"{parts[0].Substring(0, 1)}{parts[1].Substring(0, 1)}".ToUpper();
        }

        private string GetGroupAvatar()
        {
            return IsGroup ? "👥" : "?";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Domain model for chat participants
    /// </summary>
    public class ChatParticipant
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime JoinedDate { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Username : Name;
        public string StatusText => IsOnline ? "Online" : LastSeen?.ToString("Last seen HH:mm") ?? "";
    }

    /// <summary>
    /// Domain model for chat messages - business logic representation
    /// </summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        private bool _isDelivered = true;
        private bool _isFailed;
        private bool _isEdited;

        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public Guid ConversationId { get; set; }
        public DateTime Date { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public ChatMessageType Type { get; set; }
        public string Content { get; set; } = string.Empty;

        // UI-specific properties
        public bool IsSentByCurrentUser { get; set; }
        public bool IsGroupMessage { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public bool IsLastMessage { get; set; }

        // Message state properties
        public bool IsDelivered
        {
            get => _isDelivered;
            set
            {
                _isDelivered = value;
                OnPropertyChanged(nameof(IsDelivered));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        public bool IsFailed
        {
            get => _isFailed;
            set
            {
                _isFailed = value;
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        public bool IsEdited
        {
            get => _isEdited;
            set
            {
                _isEdited = value;
                OnPropertyChanged(nameof(IsEdited));
                OnPropertyChanged(nameof(EditedIndicator));
            }
        }

        // Reply message reference
        public ChatMessage? ReplyToMessage { get; set; }

        // Computed properties for UI
        public string FormattedTime => Date.ToString("HH:mm");
        public string FormattedDate => Date.ToString("MMM dd, yyyy");
        public bool IsToday => Date.Date == DateTime.Today;
        public bool IsYesterday => Date.Date == DateTime.Today.AddDays(-1);

        public string StatusIcon => IsFailed ? "❌" : IsDelivered ? "✓" : "⏳";
        public string EditedIndicator => IsEdited ? "(edited)" : "";

        public string MessageAlignment => IsSentByCurrentUser ? "right" : "left";
        public string MessageBubbleClass => IsSentByCurrentUser ? "message-sent" : "message-received";

        // Methods
        public string GetPreviewText(int maxLength = 50)
        {
            return Type switch
            {
                ChatMessageType.Text => Content.Length > maxLength
                    ? Content.Substring(0, maxLength) + "..."
                    : Content,
                ChatMessageType.Image => "📷 Photo",
                ChatMessageType.Video => "🎥 Video",
                ChatMessageType.Audio => "🎵 Audio",
                ChatMessageType.Sticker => "😊 Sticker",
                ChatMessageType.Post => "📝 Shared Post",
                ChatMessageType.Comment => "💬 Comment",
                ChatMessageType.AdminAction => $"ℹ️ {Content}",
                _ => Content
            };
        }

        public bool CanEdit()
        {
            return IsSentByCurrentUser &&
                   Type == ChatMessageType.Text &&
                   DateTime.UtcNow.Subtract(Date).TotalMinutes <= 15; // 15 minute edit window
        }

        public bool CanDelete()
        {
            return IsSentByCurrentUser || IsGroupMessage; // Group admins can delete others' messages
        }

        public bool CanReply()
        {
            return Type != ChatMessageType.AdminAction;
        }

        public void MarkAsDelivered()
        {
            IsDelivered = true;
            IsFailed = false;
        }

        public void MarkAsFailed()
        {
            IsFailed = true;
            IsDelivered = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Message types enum - business logic representation
    /// </summary>
    public enum ChatMessageType
    {
        AdminAction = 0,
        Sticker = 1,
        Text = 2,
        Image = 3,
        Comment = 4,
        Post = 5,
        Audio = 6,
        Video = 7
    }

    /// <summary>
    /// Chat conversation creation request
    /// </summary>
    public class CreateConversationRequest
    {
        public string? Name { get; set; } // For group chats
        public List<Guid> ParticipantIds { get; set; } = new();
        public bool IsGroup { get; set; }
        public Guid? DirectMessageUserId { get; set; } // For direct messages
    }

    /// <summary>
    /// Send message request
    /// </summary>
    public class SendMessageRequest
    {
        public Guid ConversationId { get; set; }
        public string Content { get; set; } = string.Empty;
        public ChatMessageType Type { get; set; } = ChatMessageType.Text;
        public Guid? ReplyToMessageId { get; set; }

        // Temporary ID for optimistic UI updates
        public Guid? TemporaryId { get; set; }
    }

    /// <summary>
    /// Paginated messages response
    /// </summary>
    public class ChatMessagesResult
    {
        public List<ChatMessage> Messages { get; set; } = new();
        public int TotalPages { get; set; }
        public long TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreMessages => CurrentPage < TotalPages;
    }

    /// <summary>
    /// Chat search request
    /// </summary>
    public class ChatSearchRequest
    {
        public string SearchTerm { get; set; } = string.Empty;
        public Guid? ConversationId { get; set; } // Search within specific conversation
        public ChatMessageType? MessageType { get; set; } // Filter by message type
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}