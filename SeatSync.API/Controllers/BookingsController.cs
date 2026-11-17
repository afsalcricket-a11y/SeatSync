using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SeatSync.API.Data;
using SeatSync.API.DTOs;
using SeatSync.API.Services;

namespace SeatSync.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("booking-limit")]
public class BookingsController : ControllerBase
{
    private readonly BookingService _bookingService;
    private readonly AppDbContext _db;

    public BookingsController(BookingService bookingService, AppDbContext db)
    {
        _bookingService = bookingService;
        _db = db;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("lock")]
    public async Task<IActionResult> LockSeat(LockSeatRequest req)
    {
        var result = await _bookingService.LockSeatAsync(req.SeatId, GetUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmBooking(ConfirmBookingRequest req)
    {
        var result = await _bookingService.ConfirmBookingAsync(req.SeatId, GetUserId(), req.IdempotencyKey);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("my")]
    public async Task<IActionResult> MyBookings()
    {
        var userId = GetUserId();
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        var bookings = await _db.Bookings
            .Include(b => b.Seat)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id,
                Seat = $"{b.Seat!.RowLabel}{b.Seat.SeatNumber}",
                b.Seat.Price,
                b.Status,
                b.IdempotencyKey,
                b.CreatedAt
            })
            .ToListAsync();

        return Ok(new { walletBalance = wallet?.Balance ?? 0m, bookings });
    }
}
