using Microsoft.EntityFrameworkCore;
using PrivateGym.Api.Data;
using PrivateGym.Api.Models;

namespace PrivateGym.Api.Services
{
    /// Фоновый сервис (BackgroundService) для автоматического обновления статусов бронирований.
    /// Запускается при старте приложения и работает в отдельном потоке на протяжении всего жизненного цикла.
    public class BookingStatusUpdateService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingStatusUpdateService> _logger;

        public BookingStatusUpdateService(
            IServiceProvider serviceProvider,
            ILogger<BookingStatusUpdateService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Фоновый сервис обновления статусов бронирований запущен");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Создаём отдельный scope для каждого цикла, чтобы корректно управлять временем жизни DbContext
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var now = DateTimeOffset.UtcNow;

                    // Ищем подтверждённые брони, время окончания которых уже прошло.
                    // Они переводятся в статус Completed для корректного отображения в статистике и отчётах.
                    var expiredBookings = await context.Bookings
                        .Where(b =>
                            b.Status == BookingStatus.Confirmed &&
                            b.EndTime < now)
                        .ToListAsync(stoppingToken);

                    if (expiredBookings.Count > 0)
                    {
                        foreach (var booking in expiredBookings)
                        {
                            booking.Status = BookingStatus.Completed;
                            _logger.LogInformation($"Бронирование #{booking.Id} автоматически помечено как Completed");
                        }

                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"Пакетно обновлено {expiredBookings.Count} бронирований");
                    }
                }
                catch (Exception ex)
                {
                    // Логируем ошибку, но не прерываем работу сервиса, чтобы он продолжил попытки в следующем цикле
                    _logger.LogError(ex, "❌ Ошибка в фоновом сервисе обновления статусов");
                }

                // Пауза 5 минут перед следующей итерацией, чтобы не создавать постоянную нагрузку на базу данных
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}