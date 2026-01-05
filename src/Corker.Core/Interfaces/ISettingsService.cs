using Corker.Core.Settings;

namespace Corker.Core.Interfaces;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}
