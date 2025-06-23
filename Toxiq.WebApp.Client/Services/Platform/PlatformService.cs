using Microsoft.JSInterop;
using System.Text.Json;

namespace Toxiq.WebApp.Client.Services.Platform
{
    public interface IPlatformService
    {
        ValueTask<bool> IsTelegramMiniAppAsync();
        ValueTask<bool> IsDesktopAsync();
        ValueTask<bool> IsMobileAsync();
        ValueTask<PlatformInfo> GetPlatformInfoAsync();
    }

    public record PlatformInfo(
        bool IsTelegramMiniApp,
        bool IsDesktop,
        bool IsMobile,
        string UserAgent,
        int ViewportWidth,
        int ViewportHeight
    );

    public class PlatformService : IPlatformService
    {
        private readonly IJSRuntime _jsRuntime;
        private PlatformInfo _cachedInfo;

        public PlatformService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async ValueTask<bool> IsTelegramMiniAppAsync()
        {
            var info = await GetPlatformInfoAsync();
            return info.IsTelegramMiniApp;
        }

        public async ValueTask<bool> IsDesktopAsync()
        {
            var info = await GetPlatformInfoAsync();
            return info.IsDesktop;
        }

        public async ValueTask<bool> IsMobileAsync()
        {
            var info = await GetPlatformInfoAsync();
            return info.IsMobile;
        }

        public async ValueTask<PlatformInfo> GetPlatformInfoAsync()
        {
            if (_cachedInfo != null)
                return _cachedInfo;

            try
            {
                var result = await _jsRuntime.InvokeAsync<JsonElement>("window.toxiqPlatform.detect");

                _cachedInfo = new PlatformInfo(
                    IsTelegramMiniApp: result.GetProperty("isTelegramMiniApp").GetBoolean(),
                    IsDesktop: result.GetProperty("isDesktop").GetBoolean(),
                    IsMobile: result.GetProperty("isMobile").GetBoolean(),
                    UserAgent: result.GetProperty("userAgent").GetString(),
                    ViewportWidth: result.GetProperty("viewportWidth").GetInt32(),
                    ViewportHeight: result.GetProperty("viewportHeight").GetInt32()
                );

                return _cachedInfo;
            }
            catch (Exception)
            {
                // Fallback detection
                _cachedInfo = new PlatformInfo(false, true, false, "", 1920, 1080);
                return _cachedInfo;
            }
        }
    }
}
