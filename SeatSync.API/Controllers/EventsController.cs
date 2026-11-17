using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SeatSync.API.Data;
using SeatSync.API.DTOs;

namespace SeatSync.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public EventsController(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetEvents()
    {
        var events = await _db.Events
            .Select(e => new { e.Id, e.Title, e.Venue, e.EventDate })
            .ToListAsync();
        return Ok(events);
    }

    [HttpGet("{id}/seats")]
    public async Task<IActionResult> GetEventSeats(int id)
    {
        var cacheKey = $"event_seats_{id}";
        if (!_cache.TryGetValue(cacheKey, out List<SeatDto>? seats))
        {
            seats = await _db.Seats
                .Where(s => s.EventId == id)
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .Select(s => new SeatDto(
                    s.Id,
                    s.RowLabel,
                    s.SeatNumber,
                    s.Price,
                    s.Status == "Locked" && s.LockExpiresAt <= DateTime.UtcNow ? "Available" : s.Status,
                    s.LockedByUserId,
                    s.LockExpiresAt))
                .ToListAsync();

            _cache.Set(cacheKey, seats, TimeSpan.FromSeconds(15));
        }

        return Ok(seats);
    }
}
