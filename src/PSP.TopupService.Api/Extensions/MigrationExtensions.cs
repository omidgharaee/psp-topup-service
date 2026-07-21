using Microsoft.EntityFrameworkCore;
using PSP.TopupService.Persistence.Context;

namespace PSP.TopupService.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<TopupDbContext>();
            if (context.Database.IsRelational())
            {
                var logger = services.GetRequiredService<ILogger<TopupDbContext>>();
                logger.LogInformation("در حال اجرای مایگریشن‌های دیتابیس...");
                await context.Database.MigrateAsync();
                logger.LogInformation("مایگریشن‌ها با موفقیت اعمال شدند.");
            }
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "خطا در اعمال مایگریشن‌های دیتابیس");
            throw;
        }
    }
}
