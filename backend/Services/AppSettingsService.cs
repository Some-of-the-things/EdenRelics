using System.Globalization;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public interface IAppSettingsService
{
    Task<string?> GetAsync(string key);
    Task<int> GetIntAsync(string key, int fallback);
    Task SetAsync(string key, string value);
}

/// <summary>Admin-editable settings stored one row per key in <see cref="AppSetting"/>.</summary>
public class AppSettingsService(IRepository<AppSetting> settings) : IAppSettingsService
{
    public const string ReviewRequestDelayDays = "reviews.requestDelayDays";

    public async Task<string?> GetAsync(string key)
    {
        AppSetting? row = await settings.Query().FirstOrDefaultAsync(s => s.Key == key);
        return row?.Value;
    }

    public async Task<int> GetIntAsync(string key, int fallback)
    {
        string? value = await GetAsync(key);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    public async Task SetAsync(string key, string value)
    {
        AppSetting? row = await settings.Query().FirstOrDefaultAsync(s => s.Key == key);
        if (row is null)
        {
            await settings.AddAsync(new AppSetting { Key = key, Value = value });
            return;
        }
        row.Value = value;
        await settings.UpdateAsync(row);
    }
}
