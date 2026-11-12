using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateGym.Api.Data;
using PrivateGym.Api.DTOs;
using PrivateGym.Api.Models;

namespace PrivateGym.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoomsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RoomsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/rooms
        // Получение списка залов с фильтрацией и пагинацией
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<Room>>> GetRooms(
            [FromQuery] string? muscleGroup,
            [FromQuery] int? minCapacity,
            [FromQuery] decimal? maxPrice,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _context.Rooms.AsQueryable();

            // Применяем фильтрацию по группе мышц, если она указана
            if (!string.IsNullOrWhiteSpace(muscleGroup))
            {
                query = query.Where(r => r.MuscleGroups.Contains(muscleGroup));
            }

            // Фильтрация по минимальной вместимости
            if (minCapacity.HasValue)
            {
                query = query.Where(r => r.Capacity >= minCapacity.Value);
            }

            // Фильтрация по максимальной цене
            if (maxPrice.HasValue)
            {
                query = query.Where(r => r.PricePerHour <= maxPrice.Value);
            }

            // Показываем только активные залы
            query = query.Where(r => r.IsActive);

            // Получаем общее количество записей для пагинации
            var totalCount = await query.CountAsync();

            // Применяем сортировку по имени и пагинацию
            var items = await query
                .OrderBy(r => r.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Возвращаем результат с метаинформацией о пагинации
            return Ok(new PaginatedResult<Room>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        // GET: api/rooms/5
        // Получение конкретного зала по ID
        [HttpGet("{id}")]
        public async Task<ActionResult<Room>> GetRoom(int id)
        {
            // Ищем зал в базе данных
            var room = await _context.Rooms.FindAsync(id);

            // Если зал не найден, возвращаем 404
            if (room == null)
            {
                return NotFound(new { Message = $"Зал с ID {id} не найден" });
            }

            // Возвращаем найденный зал
            return Ok(room);
        }

        [HttpGet("{id}/schedule")]
        public async Task<ActionResult> GetRoomSchedule(int id, [FromQuery] DateOnly date)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound(new { Message = "Зал не найден" });

            var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var busySlots = await _context.Bookings
                .Where(b =>
                    b.RoomId == id &&
                    b.Status != BookingStatus.Cancelled &&
                    b.StartTime >= startOfDay &&
                    b.EndTime <= endOfDay)
                .Select(b => new { b.StartTime, b.EndTime })
                .ToListAsync();

            return Ok(busySlots);
        }
    }
}