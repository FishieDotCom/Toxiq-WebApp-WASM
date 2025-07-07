// Toxiq.WebApp.Client/Services/Api/NotificationService.cs
using Microsoft.JSInterop;
using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Caching;

namespace Toxiq.WebApp.Client.Services.Api
{
    /// <summary>
    /// Notification service interface - mirrors mobile app's notification functionality
    /// Reference: Toxiq.Mobile/Service/OnlineDataService/UserService.GetNotification()
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Get notifications from API - mirrors mobile app's GetNotification method
        /// Reference: Mobile UserService.GetNotification()
        /// </summary>
        Task<SearchResultDto<NotificationDto>> GetNotifications();

        /// <summary>
        /// Get unread notification count (client-side)
        /// </summary>
        Task<int> GetUnreadCount();

        /// <summary>
        /// Mark all notifications as read (client-side)
        /// </summary>
        Task MarkAllAsRead();

        /// <summary>
        /// Add new notification from SignalR (client-side)
        /// </summary>
        Task AddNewNotification(NotificationDto notification);

        /// <summary>
        /// Get cached notifications from IndexedDB
        /// </summary>
        Task<List<NotificationDto>> GetCachedNotifications();

        /// <summary>
        /// Event fired when new notification arrives
        /// </summary>
        event EventHandler<NotificationDto>? NewNotificationReceived;

        /// <summary>
        /// Event fired when unread count changes
        /// </summary>
        event EventHandler<int>? UnreadCountChanged;
    }

    /// <summary>
    /// Notification service implementation - mirrors mobile app patterns with web enhancements
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly OptimizedApiService _apiService;
        private readonly IIndexedDbService _indexedDb;
        private readonly IJSRuntime _jsRuntime;
        private readonly string _cacheKey = "toxiq_notifications";
        private readonly string _unreadCountKey = "toxiq_unread_count";
        private readonly string _lastReadKey = "toxiq_last_read_time";

        public event EventHandler<NotificationDto>? NewNotificationReceived;
        public event EventHandler<int>? UnreadCountChanged;

        // FIXED: Constructor now accepts the concrete OptimizedApiService that it actually needs
        public NotificationService(OptimizedApiService apiService, IIndexedDbService indexedDb, IJSRuntime jsRuntime)
        {
            _apiService = apiService;
            _indexedDb = indexedDb;
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Get notifications from API - exact same as mobile app
        /// Reference: Mobile UserService.GetNotification()
        /// </summary>
        public async Task<SearchResultDto<NotificationDto>> GetNotifications()
        {
            try
            {
                // Use same endpoint as mobile app
                var response = await _apiService.GetAsync<SearchResultDto<NotificationDto>>("User/GetNotification");

                if (response?.Data != null)
                {
                    // Cache notifications to IndexedDB
                    await _indexedDb.SetItemAsync(_cacheKey, response.Data);

                    // Calculate unread count based on last read time
                    await UpdateUnreadCount(response.Data);
                }

                return response ?? new SearchResultDto<NotificationDto> { Data = new List<NotificationDto>() };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching notifications: {ex.Message}");

                // Return cached notifications on error
                var cached = await GetCachedNotifications();
                return new SearchResultDto<NotificationDto> { Data = cached };
            }
        }

        /// <summary>
        /// Get unread notification count (client-side tracking)
        /// </summary>
        public async Task<int> GetUnreadCount()
        {
            try
            {
                var count = await _indexedDb.GetItemAsync<int>(_unreadCountKey);
                return count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Mark all notifications as read - client-side state management
        /// </summary>
        public async Task MarkAllAsRead()
        {
            try
            {
                // Store current timestamp as last read time
                var now = DateTime.UtcNow;
                await _indexedDb.SetItemAsync(_lastReadKey, now);

                // Reset unread count to 0
                await _indexedDb.SetItemAsync(_unreadCountKey, 0);

                // Fire event for UI updates
                UnreadCountChanged?.Invoke(this, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking notifications as read: {ex.Message}");
            }
        }

        /// <summary>
        /// Add new notification from SignalR
        /// </summary>
        public async Task AddNewNotification(NotificationDto notification)
        {
            try
            {
                // Get existing cached notifications
                var cached = await GetCachedNotifications();

                // Add new notification to the beginning
                cached.Insert(0, notification);

                // Keep only latest 100 notifications in cache
                if (cached.Count > 100)
                {
                    cached = cached.Take(100).ToList();
                }

                // Update cache
                await _indexedDb.SetItemAsync(_cacheKey, cached);

                // Update unread count
                var currentCount = await GetUnreadCount();
                var newCount = currentCount + 1;
                await _indexedDb.SetItemAsync(_unreadCountKey, newCount);

                // Fire events for UI updates
                NewNotificationReceived?.Invoke(this, notification);
                UnreadCountChanged?.Invoke(this, newCount);

                // Show browser notification if supported
                await ShowBrowserNotification(notification);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding new notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Get cached notifications from IndexedDB
        /// </summary>
        public async Task<List<NotificationDto>> GetCachedNotifications()
        {
            try
            {
                var cached = await _indexedDb.GetItemAsync<List<NotificationDto>>(_cacheKey);
                return cached ?? new List<NotificationDto>();
            }
            catch
            {
                return new List<NotificationDto>();
            }
        }

        /// <summary>
        /// Update unread count based on notifications and last read time
        /// </summary>
        private async Task UpdateUnreadCount(List<NotificationDto> notifications)
        {
            try
            {
                var lastReadTime = await _indexedDb.GetItemAsync<DateTime?>(_lastReadKey);

                int unreadCount = 0;
                if (lastReadTime.HasValue)
                {
                    unreadCount = notifications.Count(n => n.Date > lastReadTime.Value);
                }
                else
                {
                    // If no last read time, all notifications are unread
                    unreadCount = notifications.Count;
                }

                await _indexedDb.SetItemAsync(_unreadCountKey, unreadCount);
                UnreadCountChanged?.Invoke(this, unreadCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating unread count: {ex.Message}");
            }
        }

        /// <summary>
        /// Show browser notification for new notifications
        /// </summary>
        private async Task ShowBrowserNotification(NotificationDto notification)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("toxiq.notifications.show",
                    "Toxiq",
                    notification.Text ?? "New notification",
                    "/favicon.ico");
            }
            catch
            {
                // Browser notifications might not be supported or permitted
                // Fail silently
            }
        }
    }
}