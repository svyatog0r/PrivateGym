using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateGym.Api.Data;
using PrivateGym.Api.Models;
using PrivateGym.Api.DTOs;
using System.Globalization;
using System.Data;
using Npgsql;

namespace PrivateGym.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/admin/rooms - получить все залы (включая неактивные)
        [HttpGet("rooms")]
        public async Task<ActionResult<List<Room>>> GetAllRooms()
        {
            var rooms = await _context.Rooms.OrderBy(r => r.Name).ToListAsync();
            return Ok(rooms);
        }

        // POST: api/admin/rooms - добавить новый зал
        [HttpPost("rooms")]
        public async Task<ActionResult<Room>> CreateRoom([FromBody] Room room)
        {
            room.IsActive = true;

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAllRooms), new { }, room);
        }

        // DELETE: api/admin/rooms/5 - удалить зал
        [HttpDelete("rooms/{id}")]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound(new { Message = "Зал не найден" });
            }

            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Зал успешно удален" });
        }

        // GET: api/admin/bookings
        [HttpGet("bookings")]
        public async Task<ActionResult> GetAllBookings()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.User)
                .Where(b => b.Status != BookingStatus.Cancelled)
                .OrderByDescending(b => b.StartTime)
                .Select(b => new
                {
                    b.Id,
                    b.RoomId,
                    RoomName = b.Room.Name,
                    UserId = b.UserId,
                    UserName = b.User.FullName,
                    UserEmail = b.User.Email,
                    b.StartTime,
                    b.EndTime,
                    b.Status,
                    b.TotalPrice,
                    b.PeopleCount,
                    b.GuestNames
                })
                .ToListAsync();

            return Ok(bookings);
        }

        // DELETE: api/admin/bookings/5/delete - полностью удалить запись из базы данных
        [HttpDelete("bookings/{id}/delete")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound(new { Message = "Бронирование не найдено" });
            }

            // Полностью удаляем запись из базы данных
            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Бронирование успешно удалено из базы данных" });
        }

        // DELETE: api/admin/bookings/5 - отменить бронирование
        [HttpDelete("bookings/{id}")]
        public async Task<IActionResult> CancelBookingAdmin(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound(new { Message = "Бронирование не найдено" });
            }

            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
            {
                return BadRequest(new { Message = "Это бронирование нельзя отменить" });
            }

            booking.Status = BookingStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Бронирование успешно отменено" });
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<AdminStatisticsDto>> GetStatistics(
    [FromQuery] string period = "all",
    [FromQuery] string startDate = null,
    [FromQuery] string endDate = null)
        {
            DateTimeOffset start;
            DateTimeOffset end;
            DateTimeOffset prevStart;
            DateTimeOffset prevEnd;

            var now = DateTimeOffset.UtcNow;

            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                if (DateTime.TryParse(startDate, out DateTime sDate) && DateTime.TryParse(endDate, out DateTime eDate))
                {
                    start = new DateTimeOffset(sDate.Date, TimeSpan.Zero);
                    end = new DateTimeOffset(eDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);

                    var duration = end - start;
                    prevEnd = start;
                    prevStart = prevEnd - duration;
                }
                else
                {
                    return BadRequest("Неверный формат даты");
                }
            }
            else
            {
                if (period == "month")
                {
                    start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                    end = now;
                    prevStart = start.AddMonths(-1);
                    prevEnd = prevStart + (end - start); 
                }
                else if (period == "3months")
                {
                    start = now.AddMonths(-3);
                    end = now;
                    prevStart = start.AddMonths(-3);
                    prevEnd = start;                     
                }
                else if (period == "year")
                {
                    start = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    end = now;
                    prevStart = new DateTimeOffset(now.Year - 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    prevEnd = prevStart + (end - start); 
                }
                else
                {
                    start = DateTimeOffset.MinValue;
                    end = now;
                    prevStart = DateTimeOffset.MinValue;
                    prevEnd = DateTimeOffset.MinValue;
                }
            }

            var query = _context.Bookings.Where(b => b.StartTime >= start && b.StartTime <= end);
            var totalBookings = await query.CountAsync();
            var totalRevenue = await query.Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed).SumAsync(b => (decimal?)b.TotalPrice) ?? 0;
            var activeBookings = await query.CountAsync(b => b.Status == BookingStatus.Confirmed);

            double? revenueChange = null;
            double? bookingsChange = null;

            if (period != "all" || (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate)))
            {
                var prevQuery = _context.Bookings.Where(b => b.StartTime >= prevStart && b.StartTime < prevEnd);
                var prevBookings = await prevQuery.CountAsync();
                var prevRevenue = await prevQuery.Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed).SumAsync(b => (decimal?)b.TotalPrice) ?? 0;

                revenueChange = prevRevenue > 0 ? (double)((totalRevenue - prevRevenue) / prevRevenue * 100) : (totalRevenue > 0 ? 100 : 0);
                bookingsChange = prevBookings > 0 ? (double)((totalBookings - prevBookings) / prevBookings * 100) : (totalBookings > 0 ? 100 : 0);
            }

            var totalRooms = await _context.Rooms.CountAsync(r => r.IsActive);

            var chartEnd = end;
            var chartStart = start;

            if ((chartEnd - chartStart).TotalDays > 31)
            {
                chartStart = chartEnd.AddDays(-31);
            }

            var chartBookings = await _context.Bookings
                .Where(b => b.StartTime >= chartStart && b.StartTime <= chartEnd
                       && b.Status != BookingStatus.Cancelled)
                .Select(b => new { b.StartTime, b.TotalPrice, b.Status, b.PeopleCount })
                .ToListAsync();

            var chartData = chartBookings
                .GroupBy(b => $"{b.StartTime.Year}-{b.StartTime.Month:D2}-{b.StartTime.Day:D2}")
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var parts = g.Key.Split('-');
                    int year = int.Parse(parts[0]);
                    int month = int.Parse(parts[1]);
                    int day = int.Parse(parts[2]);

                    return new ChartPointDto
                    {
                        Label = new DateTime(year, month, day).ToString("dd MMM", new CultureInfo("ru-RU")),
                        Bookings = g.Count(),
                        TotalPeople = g.Sum(x => x.PeopleCount),
                        Revenue = g.Where(x => x.Status == BookingStatus.Confirmed || x.Status == BookingStatus.Completed).Sum(x => x.TotalPrice)
                    };
                })
                .ToList();

            return Ok(new AdminStatisticsDto
            {
                TotalBookings = totalBookings,
                TotalRevenue = totalRevenue,
                ActiveBookings = activeBookings,
                TotalRooms = totalRooms,
                RevenueChange = revenueChange,
                BookingsChange = bookingsChange,
                ChartData = chartData
            });
        }
        // PUT: api/admin/rooms/5 - обновить зал
        [HttpPut("rooms/{id}")]
        public async Task<IActionResult> UpdateRoom(int id, [FromBody] Room room)
        {
            if (id != room.Id)
            {
                return BadRequest(new { Message = "Несоответствие ID" });
            }

            var existingRoom = await _context.Rooms.FindAsync(id);
            if (existingRoom == null)
            {
                return NotFound(new { Message = "Зал не найден" });
            }

            existingRoom.Name = room.Name;
            existingRoom.Description = room.Description;
            existingRoom.Capacity = room.Capacity;
            existingRoom.PricePerHour = room.PricePerHour; 
            existingRoom.BasePrice = room.BasePrice;
            existingRoom.PricePerAdditionalPerson = room.PricePerAdditionalPerson;
            existingRoom.Category = room.Category;
            existingRoom.ImageUrl = room.ImageUrl;
            existingRoom.Equipment = room.Equipment ?? new List<string>();
            existingRoom.MuscleGroups = room.MuscleGroups ?? new List<string>();
            existingRoom.IsActive = room.IsActive;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RoomExists(id))
                {
                    return NotFound(new { Message = "Зал не найден" });
                }
                else
                {
                    throw;
                }
            }

            return Ok(existingRoom);
        }

        // PUT: api/admin/bookings/5/status - изменить статус бронирования
        [HttpPut("bookings/{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound(new { Message = "Бронирование не найдено" });
            }

            // Проверяем, что новый статус допустим
            if (!Enum.IsDefined(typeof(BookingStatus), dto.Status))
            {
                return BadRequest(new { Message = "Недопустимый статус" });
            }

            booking.Status = dto.Status;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Статус бронирования изменён на {dto.Status}" });
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.Id == id);
        }

        // GET: api/admin/users?search=иван
        [HttpGet("users")]
        public async Task<ActionResult> GetUsers([FromQuery] string? search)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
            }

            var users = await query
                .Select(u => new { u.Id, u.FullName, u.Email })
                .Take(20) 
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("bookings")]
        public async Task<IActionResult> CreateBookingForClient([FromBody] AdminBookingDto dto)
        {
            if (dto.StartTime >= dto.EndTime)
                return BadRequest(new { Message = "Время начала должно быть раньше времени окончания" });

            var room = await _context.Rooms.FindAsync(dto.RoomId);
            if (room == null || !room.IsActive)
                return NotFound(new { Message = "Зал не найден или неактивен" });

            var userExists = await _context.Users.AnyAsync(u => u.Id == dto.UserId);
            if (!userExists)
                return BadRequest(new { Message = "Клиент не найден" });

            // Единый подход с BookingsController: считаем, что dto уже в UTC
            // (фронт присылает .toISOString()).
            var startTimeOffset = new DateTimeOffset(dto.StartTime, TimeSpan.Zero);
            var endTimeOffset = new DateTimeOffset(dto.EndTime, TimeSpan.Zero);

            var hours = (dto.EndTime - dto.StartTime).TotalHours;
            var pricePerHour = room.BasePrice + room.PricePerAdditionalPerson * (dto.PeopleCount - 1);
            var totalPrice = pricePerHour * (decimal)hours;

            await using var tx = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var overlap = await _context.Bookings.AnyAsync(b =>
                    b.RoomId == dto.RoomId &&
                    b.Status != BookingStatus.Cancelled &&
                    b.StartTime < endTimeOffset &&
                    b.EndTime > startTimeOffset);

                if (overlap)
                {
                    await tx.RollbackAsync();
                    return Conflict(new { Message = "Зал уже занят на это время" });
                }

                var booking = new Booking
                {
                    UserId = dto.UserId,
                    RoomId = dto.RoomId,
                    StartTime = startTimeOffset,
                    EndTime = endTimeOffset,
                    Status = BookingStatus.Confirmed,
                    TotalPrice = totalPrice,
                    PeopleCount = dto.PeopleCount,
                    GuestUserIds = dto.GuestUserIds,
                    GuestNames = dto.GuestNames
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new { Message = "Бронирование успешно создано", Id = booking.Id });
            }
            catch (Exception ex) when (
                ex is DbUpdateException
                || (ex is PostgresException pg && pg.SqlState == PostgresErrorCodes.SerializationFailure))
            {
                await tx.RollbackAsync();
                return Conflict(new { Message = "Слот только что заняли. Обновите расписание." });
            }
        }
    }
}