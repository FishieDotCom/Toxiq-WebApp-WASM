using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Toxiq.WebApp.Client.Services.LazyLoading
{
    public interface ILazyLoader
    {
        ValueTask<bool> LoadModuleAsync(string moduleName);
        ValueTask<T> LoadComponentAsync<T>(string moduleName, string componentName) where T : ComponentBase;
    }

    public class LazyLoader : ILazyLoader
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<LazyLoader> _logger;
        private readonly HashSet<string> _loadedModules = new();

        public LazyLoader(IJSRuntime jsRuntime, ILogger<LazyLoader> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async ValueTask<bool> LoadModuleAsync(string moduleName)
        {
            if (_loadedModules.Contains(moduleName))
                return true;

            try
            {
                await _jsRuntime.InvokeVoidAsync("import", $"./_content/Toxiq.WebApp.Client/modules/{moduleName}.js");
                _loadedModules.Add(moduleName);
                _logger.LogInformation("Successfully loaded module: {ModuleName}", moduleName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load module: {ModuleName}", moduleName);
                return false;
            }
        }

        public async ValueTask<T> LoadComponentAsync<T>(string moduleName, string componentName) where T : ComponentBase
        {
            await LoadModuleAsync(moduleName);

            // Implementation would depend on your specific lazy loading strategy
            // This is a simplified version
            var type = Type.GetType($"Toxiq.WebApp.Client.Components.{moduleName}.{componentName}");
            return (T)Activator.CreateInstance(type);
        }
    }
}
