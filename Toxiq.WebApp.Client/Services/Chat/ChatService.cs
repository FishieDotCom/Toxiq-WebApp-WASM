// Toxiq.WebApp.Client/Services/Chat/ChatService.cs
// Chat service implementation using domain models - clean business logic
// Reference: Toxiq.Mobile/Service/OnlineDataService/ChatService.cs

using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Domain.Chat.Mappers;
using Toxiq.WebApp.Client.Domain.Chat.Models;
using Toxiq.WebApp.Client.Services.Api;
using Toxiq.WebApp.Client.Services.Authentication;
using Toxiq.WebApp.Client.Services.Caching;
using Toxiq.WebApp.Client.Services.SignalR;

namespace Toxiq.WebApp.Client.Services.Chat
{
    /// <summary>
    /// Chat service interface - business logic layer using domain models
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// Gets all conversations for the current user
        /// </summary>
        Task<List<ChatConversation>> GetConversationsAsync(bool forceRefresh = false);

        /// <summary>
        /// Gets a specific conversation by ID
        /// </summary>
        Task<ChatConversation?> GetConversationAsync(Guid conversationId);

        /// <summary>
        /// Gets messages for a specific conversation with pagination
        /// </summary>
        Task<ChatMessagesResult> GetMessagesAsync(Guid conversationId, int page = 1, int pageSize = 20);

        /// <summary>
        /// Sends a message to a conversation with optimistic UI updates
        /// </summary>
        Task<ChatMessage> SendMessageAsync(SendMessageRequest request);

        /// <summary>
        /// Creates a direct message conversation with another user
        /// </summary>
        Task<ChatConversation> CreateDirectConversationAsync(Guid userId);

        /// <summary>
        /// Creates a group conversation
        /// </summary>
        Task<ChatConversation> CreateGroupConversationAsync(CreateConversationRequest request);

        /// <summary>
        /// Search messages within conversations
        /// </summary>
        Task<ChatMessagesResult> SearchMessagesAsync(ChatSearchRequest request);

        /// <summary>
        /// Mark conversation as read
        /// </summary>
        Task MarkConversationAsReadAsync(Guid conversationId);

        /// <summary>
        /// Delete a message (if user has permission)
        /// </summary>
        Task<bool> DeleteMessageAsync(Guid messageId);

        /// <summary>
        /// Edit a message (if within time limit)
        /// </summary>
        Task<ChatMessage?> EditMessageAsync(Guid messageId, string newContent);

        /// <summary>
        /// Get unread message count across all conversations
        /// </summary>
        Task<int> GetUnreadMessageCountAsync();

        /// <summary>
        /// Event fired when new message arrives
        /// </summary>
        event EventHandler<ChatMessage>? NewMessageReceived;

        /// <summary>
        /// Event fired when conversation list updates
        /// </summary>
        event EventHandler<List<ChatConversation>>? ConversationsUpdated;

        /// <summary>
        /// Event fired when message is updated (edited, deleted, etc.)
        /// </summary>
        event EventHandler<ChatMessage>? MessageUpdated;
    }

    /// <summary>
    /// Chat service implementation - handles business logic with domain models
    /// </summary>
    public class ChatService : IChatService, IDisposable
    {
        private readonly OptimizedApiService _apiService;
        private readonly IIndexedDbService _indexedDb;
        private readonly ILogger<ChatService> _logger;
        private readonly ISignalRService _signalRService;
        private readonly IAuthenticationService _authService;

        // Cache keys matching mobile app patterns
        private readonly string ConversationsCacheKey = "chat_conversations";
        private readonly string ConversationCacheKeyPrefix = "conversation_";
        private readonly string MessagesCacheKeyPrefix = "messages_";
        private readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

        // In-memory cache for active conversations
        private readonly Dictionary<Guid, ChatConversation> _conversationCache = new();
        private readonly Dictionary<Guid, List<ChatMessage>> _messageCache = new();

        public event EventHandler<ChatMessage>? NewMessageReceived;
        public event EventHandler<List<ChatConversation>>? ConversationsUpdated;
        public event EventHandler<ChatMessage>? MessageUpdated;

        public ChatService(
            OptimizedApiService apiService,
            IIndexedDbService indexedDb,
            ILogger<ChatService> logger,
            ISignalRService signalRService,
            IAuthenticationService authService)
        {
            _apiService = apiService;
            _indexedDb = indexedDb;
            _logger = logger;
            _signalRService = signalRService;
            _authService = authService;

            // Subscribe to SignalR events for real-time chat
            _signalRService.MessageReceived += OnSignalRMessageReceived;
            _signalRService.MessageUpdated += OnSignalRMessageUpdated;
            _signalRService.ConversationUpdated += OnSignalRConversationUpdated;
        }

        public async Task<List<ChatConversation>> GetConversationsAsync(bool forceRefresh = false)
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == Guid.Empty) return new List<ChatConversation>();

                List<ChatConversation> conversations;

                if (!forceRefresh)
                {
                    // Try cache first
                    var cachedConversations = await GetCachedConversationsAsync();
                    if (cachedConversations.Any())
                    {
                        _logger?.LogDebug("Returning {Count} conversations from cache", cachedConversations.Count);
                        return cachedConversations;
                    }
                }

                // Fetch from API
                _logger?.LogDebug("Fetching conversations from API");
                var conversationDtos = await _apiService.GetAsync<List<Conversation>>("Chat/conversations");

                if (conversationDtos == null)
                {
                    _logger?.LogWarning("No conversations returned from API");
                    return new List<ChatConversation>();
                }

                // Convert to domain models
                conversations = conversationDtos.ToDomainList(currentUserId);

                // Cache the results
                await CacheConversationsAsync(conversations);

                // Update in-memory cache
                foreach (var conversation in conversations)
                {
                    _conversationCache[conversation.Id] = conversation;
                }

                _logger?.LogDebug("Fetched {Count} conversations from API", conversations.Count);

                // Fire update event
                ConversationsUpdated?.Invoke(this, conversations);

                return conversations.SortByLastMessage();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching conversations");

                // Return cached data on error
                var cachedData = await GetCachedConversationsAsync();
                return cachedData;
            }
        }

        public async Task<ChatConversation?> GetConversationAsync(Guid conversationId)
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == Guid.Empty) return null;

                // Check in-memory cache first
                if (_conversationCache.TryGetValue(conversationId, out var cachedConversation))
                {
                    return cachedConversation;
                }

                // Check IndexedDB cache
                var cacheKey = $"{ConversationCacheKeyPrefix}{conversationId}";
                var cachedDto = await _indexedDb.GetItemAsync<Conversation>(cacheKey);
                if (cachedDto != null)
                {
                    var domainModel = cachedDto.ToDomain(currentUserId);
                    _conversationCache[conversationId] = domainModel;
                    return domainModel;
                }

                // Fetch from API
                var conversationDto = await _apiService.GetAsync<Conversation>($"Chat/conversations/{conversationId}");
                if (conversationDto == null) return null;

                var conversation = conversationDto.ToDomain(currentUserId);

                // Cache the result
                await _indexedDb.SetItemAsync(cacheKey, conversationDto);
                _conversationCache[conversationId] = conversation;

                return conversation;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching conversation {ConversationId}", conversationId);
                return null;
            }
        }

        public async Task<ChatMessagesResult> GetMessagesAsync(Guid conversationId, int page = 1, int pageSize = 20)
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == Guid.Empty) return new ChatMessagesResult();

                // Get conversation for context
                var conversation = await GetConversationAsync(conversationId);

                // Check cache for first page
                if (page == 1)
                {
                    var cacheKey = $"{MessagesCacheKeyPrefix}{conversationId}_page_{page}";
                    var cachedResponse = await _indexedDb.GetItemAsync<MessageResponse>(cacheKey);
                    if (cachedResponse != null)
                    {
                        var cachedResult = cachedResponse.ToDomain(currentUserId, conversation);

                        // Update in-memory cache
                        _messageCache[conversationId] = cachedResult.Messages;

                        return cachedResult;
                    }
                }

                // Fetch from API
                var responseDto = await _apiService.GetAsync<MessageResponse>(
                    $"Chat/conversations/{conversationId}/messages?page={page}&pageSize={pageSize}");

                if (responseDto == null)
                {
                    return new ChatMessagesResult
                    {
                        Messages = new List<ChatMessage>(),
                        CurrentPage = page,
                        PageSize = pageSize
                    };
                }

                var result = responseDto.ToDomain(currentUserId, conversation);

                // Cache first page
                if (page == 1)
                {
                    var cacheKey = $"{MessagesCacheKeyPrefix}{conversationId}_page_{page}";
                    await _indexedDb.SetItemAsync(cacheKey, responseDto);
                    _messageCache[conversationId] = result.Messages;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching messages for conversation {ConversationId}", conversationId);
                return new ChatMessagesResult();
            }
        }

        public async Task<ChatMessage> SendMessageAsync(SendMessageRequest request)
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                var currentUser = await _authService.GetCurrentUserAsync();

                if (currentUserId == Guid.Empty || currentUser == null)
                    throw new InvalidOperationException("User not authenticated");

                // Create optimistic message for immediate UI feedback
                var optimisticMessage = ChatMappers.CreateOptimisticMessage(
                    request,
                    currentUserId,
                    currentUser.Name ?? currentUser.UserName);

                // Add to local cache immediately for optimistic UI
                if (_messageCache.TryGetValue(request.ConversationId, out var messages))
                {
                    messages.Insert(0, optimisticMessage);
                }

                // Fire event for immediate UI update
                NewMessageReceived?.Invoke(this, optimisticMessage);

                try
                {
                    // Send to API
                    var messageDto = request.ToDto(currentUserId);
                    var responseDto = await _apiService.PostAsync<Message>(
                        $"Chat/conversations/{request.ConversationId}/messages",
                        messageDto);

                    if (responseDto != null)
                    {
                        // Update optimistic message with server response
                        optimisticMessage.UpdateFromServerResponse(responseDto);

                        // Update conversation's last message
                        if (_conversationCache.TryGetValue(request.ConversationId, out var conversation))
                        {
                            conversation.UpdateLastMessage(optimisticMessage);
                        }

                        // Fire update event
                        MessageUpdated?.Invoke(this, optimisticMessage);

                        return optimisticMessage;
                    }
                    else
                    {
                        // Mark as failed
                        optimisticMessage.MarkAsFailed();
                        MessageUpdated?.Invoke(this, optimisticMessage);
                        return optimisticMessage;
                    }
                }
                catch (Exception apiEx)
                {
                    _logger?.LogError(apiEx, "Failed to send message to API");

                    // Mark message as failed
                    optimisticMessage.MarkAsFailed();
                    MessageUpdated?.Invoke(this, optimisticMessage);

                    return optimisticMessage;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending message");
                throw;
            }
        }

        public async Task<ChatConversation> CreateDirectConversationAsync(Guid userId)
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == Guid.Empty)
                    throw new InvalidOperationException("User not authenticated");

                // Create request
                var request = new CreateConversationRequest
                {
                    DirectMessageUserId = userId,
                    IsGroup = false,
                    ParticipantIds = new List<Guid> { userId }
                };

                // Send to API
                var conversationDto = await _apiService.PostAsync<Conversation>("Chat/conversations/direct", new { UserId = userId });

                if (conversationDto == null)
                    throw new InvalidOperationException("Failed to create conversation");

                var conversation = conversationDto.ToDomain(currentUserId);

                // Update caches
                _conversationCache[conversation.Id] = conversation;
                await InvalidateConversationsCache();

                return conversation;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating direct conversation with user {UserId}", userId);
                throw;
            }
        }

        public async Task<ChatConversation> CreateGroupConversationAsync(CreateConversationRequest request)
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == Guid.Empty)
                    throw new InvalidOperationException("User not authenticated");

                // Send to API
                var createRequest = new
                {
                    Name = request.Name,
                    ParticipantIds = request.ParticipantIds
                };

                var conversationDto = await _apiService.PostAsync<Conversation>("Chat/conversations/group", createRequest);

                if (conversationDto == null)
                    throw new InvalidOperationException("Failed to create group conversation");

                var conversation = conversationDto.ToDomain(currentUserId);

                // Update caches
                _conversationCache[conversation.Id] = conversation;
                await InvalidateConversationsCache();

                return conversation;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating group conversation");
                throw;
            }
        }

        public async Task<ChatMessagesResult> SearchMessagesAsync(ChatSearchRequest request)
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                if (currentUserId == Guid.Empty) return new ChatMessagesResult();

                var queryParams = new Dictionary<string, string>
                {
                    ["searchTerm"] = request.SearchTerm,
                    ["page"] = request.Page.ToString(),
                    ["pageSize"] = request.PageSize.ToString()
                };

                if (request.ConversationId.HasValue)
                    queryParams["conversationId"] = request.ConversationId.Value.ToString();

                if (request.MessageType.HasValue)
                    queryParams["messageType"] = ((int)request.MessageType.Value).ToString();

                if (request.FromDate.HasValue)
                    queryParams["fromDate"] = request.FromDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");

                if (request.ToDate.HasValue)
                    queryParams["toDate"] = request.ToDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");

                var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

                var responseDto = await _apiService.GetAsync<MessageResponse>($"Chat/search?{queryString}");

                if (responseDto == null) return new ChatMessagesResult();

                return responseDto.ToDomain(currentUserId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error searching messages");
                return new ChatMessagesResult();
            }
        }

        public async Task MarkConversationAsReadAsync(Guid conversationId)
        {
            try
            {
                // Update local conversation
                if (_conversationCache.TryGetValue(conversationId, out var conversation))
                {
                    conversation.MarkAsRead();
                }

                // Send to API
                await _apiService.PostRawAsync($"Chat/conversations/{conversationId}/mark-read", new { });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error marking conversation as read {ConversationId}", conversationId);
            }
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId)
        {
            try
            {
                //var result = await _apiService.DeleteAsync($"Chat/messages/{messageId}");

                //if (result)
                //{
                //    // Remove from local caches
                //    foreach (var messages in _messageCache.Values)
                //    {
                //        var message = messages.FirstOrDefault(m => m.Id == messageId);
                //        if (message != null)
                //        {
                //            messages.Remove(message);
                //            break;
                //        }
                //    }
                //}

                //return result;
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting message {MessageId}", messageId);
                return false;
            }
        }

        public async Task<ChatMessage?> EditMessageAsync(Guid messageId, string newContent)
        {
            try
            {
                var request = new { Content = newContent };
                var responseDto = await _apiService.PutAsync<Message>($"Chat/messages/{messageId}", request);

                if (responseDto == null) return null;

                var currentUserId = await GetCurrentUserIdAsync();
                var updatedMessage = responseDto.ToDomain(currentUserId);
                updatedMessage.IsEdited = true;

                // Update local cache
                foreach (var messages in _messageCache.Values)
                {
                    var existingMessage = messages.FirstOrDefault(m => m.Id == messageId);
                    if (existingMessage != null)
                    {
                        existingMessage.Content = newContent;
                        existingMessage.IsEdited = true;
                        MessageUpdated?.Invoke(this, existingMessage);
                        return existingMessage;
                    }
                }

                return updatedMessage;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error editing message {MessageId}", messageId);
                return null;
            }
        }

        public async Task<int> GetUnreadMessageCountAsync()
        {
            try
            {
                var conversations = await GetConversationsAsync();
                return conversations.Sum(c => c.UnreadCount);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting unread message count");
                return 0;
            }
        }

        #region SignalR Event Handlers

        private async void OnSignalRMessageReceived(object? sender, object messageData)
        {
            try
            {
                if (messageData is Message messageDto)
                {
                    var currentUserId = await GetCurrentUserIdAsync();
                    var message = messageDto.ToDomain(currentUserId);

                    // Add to local cache
                    if (_messageCache.TryGetValue(message.ConversationId, out var messages))
                    {
                        messages.Insert(0, message);
                    }

                    // Update conversation's last message
                    if (_conversationCache.TryGetValue(message.ConversationId, out var conversation))
                    {
                        conversation.UpdateLastMessage(message);
                    }

                    // Fire event
                    NewMessageReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling SignalR message received");
            }
        }

        private async void OnSignalRMessageUpdated(object? sender, object messageData)
        {
            try
            {
                if (messageData is Message messageDto)
                {
                    var currentUserId = await GetCurrentUserIdAsync();
                    var updatedMessage = messageDto.ToDomain(currentUserId);

                    // Update local cache
                    foreach (var messages in _messageCache.Values)
                    {
                        var existingMessage = messages.FirstOrDefault(m => m.Id == updatedMessage.Id);
                        if (existingMessage != null)
                        {
                            existingMessage.Content = updatedMessage.Content;
                            existingMessage.IsEdited = updatedMessage.IsEdited;
                            MessageUpdated?.Invoke(this, existingMessage);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling SignalR message updated");
            }
        }

        private async void OnSignalRConversationUpdated(object? sender, object conversationData)
        {
            try
            {
                // Refresh conversations when there are updates
                var conversations = await GetConversationsAsync(forceRefresh: true);
                ConversationsUpdated?.Invoke(this, conversations);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling SignalR conversation updated");
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<Guid> GetCurrentUserIdAsync()
        {
            try
            {
                var user = await _authService.GetCurrentUserAsync();
                return user?.Id ?? Guid.Empty;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        private async Task<List<ChatConversation>> GetCachedConversationsAsync()
        {
            try
            {
                var cachedDtos = await _indexedDb.GetItemAsync<List<Conversation>>(ConversationsCacheKey);
                if (cachedDtos?.Any() == true)
                {
                    var currentUserId = await GetCurrentUserIdAsync();
                    return cachedDtos.ToDomainList(currentUserId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error reading conversations from cache");
            }

            return new List<ChatConversation>();
        }

        private async Task CacheConversationsAsync(List<ChatConversation> conversations)
        {
            try
            {
                var dtos = conversations.ToDtoList();
                await _indexedDb.SetItemAsync(ConversationsCacheKey, dtos);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error caching conversations");
            }
        }

        private async Task InvalidateConversationsCache()
        {
            try
            {
                await _indexedDb.RemoveItemAsync(ConversationsCacheKey);
                _conversationCache.Clear();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error invalidating conversations cache");
            }
        }

        #endregion

        public void Dispose()
        {
            _signalRService.MessageReceived -= OnSignalRMessageReceived;
            _signalRService.MessageUpdated -= OnSignalRMessageUpdated;
            _signalRService.ConversationUpdated -= OnSignalRConversationUpdated;
        }
    }
}