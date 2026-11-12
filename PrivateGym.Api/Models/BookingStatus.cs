namespace PrivateGym.Api.Models;

public enum BookingStatus
{
    Pending,      // 0 - Ожидает подтверждения
    Confirmed,    // 1 - Подтверждено
    Cancelled,    // 2 - Отменено
    Completed,    // 3 - Завершено (клиент пришёл)
    NoShow        // 4 - Не пришёл
}
