using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateGym.Api.Data;
using PrivateGym.Api.DTOs;
using PrivateGym.Api.Models;
using System.Security.Claims;
using System.Data;
using Npgsql;

namespace PrivateGym.Api.Controllers
{
    /// Контроллер для управления бронированиями со стороны клиента.
    /// Позволяет создавать новые брони, просматривать свои записи (включая гостевые) и отменять их.
    /// Все методы требуют авторизации (атрибут <see cref="AuthorizeAttribute"/>).
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BookingsController(AppDbContext context)
        {
            _context = context;
        }

        /// Создаёт новое бронирование зала клиентом.
        /// Формула расчёта цены: (BasePrice + PricePerAdditionalPerson * (PeopleCount - 1)) * Hours.
        /// Сервер самостоятельно рассчитывает стоимость, игнорируя любые данные о цене, пришедшие от клиента (защита от подмены).

    [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
        if (dto == null)
            return BadRequest(new { Message = "Некорректные данные запроса (пустой объект)" });

        if (dto.StartTime >= dto.EndTime)
            return BadRequest(new { Message = "Время начала должно быть раньше времени окончания" });

        if (dto.StartTime < DateTime.UtcNow)
            return BadRequest(new { Message = "Нельзя бронировать в прошлом" });

        var room = await _context.Rooms.FindAsync(dto.RoomId);
        if (room == null)
            return NotFound(new { Message = $"Зал с ID {dto.RoomId} не найден" });

        if (!room.IsActive)
            return BadRequest(new { Message = "Зал временно недоступен для бронирования" });

        if (dto.PeopleCount < 1 || dto.PeopleCount > room.Capacity)
            return BadRequest(new { Message = $"Количество человек должно быть от 1 до {room.Capacity}" });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { Message = "Пользователь не авторизован" });

        var startOffset = new DateTimeOffset(dto.StartTime, TimeSpan.Zero);
        var endOffset = new DateTimeOffset(dto.EndTime, TimeSpan.Zero);

        var hours = (dto.EndTime - dto.StartTime).TotalHours;
        var pricePerHour = room.BasePrice + room.PricePerAdditionalPerson * (dto.PeopleCount - 1);
        var totalPrice = pricePerHour * (decimal)hours;

        // Сериализуемая транзакция защищает от гонки: два одновременных запроса
        // на один слот не смогут оба увидеть его свободным.
        await using var tx = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var overlappingBookings = await _context.Bookings
                .Where(b =>
                    b.RoomId == dto.RoomId &&
                    b.Status != BookingStatus.Cancelled &&
                    b.StartTime < endOffset &&
                    b.EndTime > startOffset)
                .FirstOrDefaultAsync();

            if (overlappingBookings != null)
            {
                await tx.RollbackAsync();
                return Conflict(new
                {
                    Message = "Зал уже забронирован на это время",
                    ExistingBooking = new
                    {
                        StartTime = overlappingBookings.StartTime,
                        EndTime = overlappingBookings.EndTime
                    }
                });
            }

            var booking = new Booking
            {
                UserId = userId,
                RoomId = dto.RoomId,
                StartTime = startOffset,
                EndTime = endOffset,
                Status = BookingStatus.Confirmed,
                TotalPrice = totalPrice,
                PeopleCount = dto.PeopleCount
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new BookingResponseDto
            {
                Id = booking.Id,
                RoomId = booking.RoomId,
                RoomName = room.Name,
                StartTime = booking.StartTime.DateTime,
                EndTime = booking.EndTime.DateTime,
                Status = booking.Status.ToString(),
                TotalPrice = booking.TotalPrice,
                PeopleCount = booking.PeopleCount
            });
        }
            catch (Exception ex) when (
                ex is DbUpdateException
                || (ex is PostgresException pg && pg.SqlState == PostgresErrorCodes.SerializationFailure))
            {
                try { await tx.RollbackAsync(); } catch { /* транзакция уже завершена */ }
                return Conflict(new { Message = "Слот только что заняли. Обновите расписание." });
            }
        }

    /// Возвращает список бронирований текущего пользователя и статистику по ним.
    /// Включает как брони, созданные пользователем, так и те, куда его добавили гостем.
    [HttpGet("my")]
        public async Task<ActionResult> GetMyBookings(
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var userIdStr = userId.ToString();

            // Ищем брони, где пользователь является создателем (UserId) или добавлен как гость (GuestUserIds)
            var listQuery = _context.Bookings
                .Where(b => b.Status != BookingStatus.Cancelled &&
                           (b.UserId == userId || (b.GuestUserIds != null && b.GuestUserIds.Contains(userIdStr))))
                .Include(b => b.Room)
                .OrderByDescending(b => b.StartTime)
                .AsQueryable();

            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
            {
                var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
                listQuery = listQuery.Where(b => b.StartTime >= startUtc);
            }

            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
            {
                var endUtc = DateTime.SpecifyKind(end.Date.AddDays(1), DateTimeKind.Utc);
                listQuery = listQuery.Where(b => b.StartTime <= endUtc);
            }

            var filteredBookings = await listQuery.ToListAsync();

            var bookingsList = filteredBookings.Select(b => new BookingResponseDto
            {
                Id = b.Id,
                RoomId = b.RoomId,
                RoomName = b.Room.Name,
                StartTime = b.StartTime.DateTime,
                EndTime = b.EndTime.DateTime,
                Status = b.Status.ToString(),
                TotalPrice = b.TotalPrice,
                PeopleCount = b.PeopleCount
            }).ToList();

            // Формируем агрегированную статистику за выбранный период
            var statsQuery = _context.Bookings
                .Where(b => (b.UserId == userId || (b.GuestUserIds != null && b.GuestUserIds.Contains(userIdStr)))
                         && b.Status != BookingStatus.Cancelled);

            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var startStat))
            {
                statsQuery = statsQuery.Where(b => b.StartTime >= DateTime.SpecifyKind(startStat, DateTimeKind.Utc));
            }
            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var endStat))
            {
                statsQuery = statsQuery.Where(b => b.StartTime <= DateTime.SpecifyKind(endStat.Date.AddDays(1), DateTimeKind.Utc));
            }

            var statsData = await statsQuery.ToListAsync();

            var totalStats = new
            {
                TotalBookings = statsData.Count,
                ActiveBookings = statsData.Count(b => b.Status == BookingStatus.Confirmed),
                CompletedBookings = statsData.Count(b => b.Status == BookingStatus.Completed),
                NoShowBookings = statsData.Count(b => b.Status == BookingStatus.NoShow),
                TotalSpent = statsData.Where(b => b.Status == BookingStatus.Completed || b.Status == BookingStatus.Confirmed).Sum(b => b.TotalPrice)
            };

            return Ok(new
            {
                Bookings = bookingsList,
                TotalStats = totalStats
            });
        }

        /// Отменяет бронирование (меняет статус на Cancelled).
        /// Доступно только создателю брони.
        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound(new { Message = "Бронирование не найдено" });
            }

            // Проверка прав: отменять может только тот, кто создал бронь
            if (booking.UserId != userId)
            {
                return Forbid();
            }

            // Запрещаем отмену уже завершённых или ранее отменённых броней
            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
            {
                return BadRequest(new { Message = "Это бронирование нельзя отменить" });
            }

            booking.Status = BookingStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Бронирование успешно отменено" });
        }
    }
}