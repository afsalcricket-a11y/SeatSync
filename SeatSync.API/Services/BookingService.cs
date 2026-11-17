using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SeatSync.API.Data;
using SeatSync.API.DTOs;
using SeatSync.API.Hubs;
using SeatSync.API.Models;

namespace SeatSync.API.Services;

public class BookingService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<SeatHub> _hub;

    public BookingService(AppDbContext db, IMemoryCache cache, IHubContext<SeatHub> hub)
    {
        _db = db;
        _cache = cache;
        _hub = hub;
    }

    public async Task<ServiceResult<SeatDto>> LockSeatAsync(int seatId, int userId)
    {
        var seat = await _db.Seats.FirstOrDefaultAsync(s => s.Id == seatId);
        if (seat == null)
            return new ServiceResult<SeatDto>(false, 404, "Seat not found.");

        // Release expired lock automatically
        if (seat.Status == "Locked" && seat.LockExpiresAt <= DateTime.UtcNow)
        {
            seat.Status = "Available";
            seat.LockedByUserId = null;
            seat.LockExpiresAt = null;
        }

        if (seat.Status == "Booked")
            return new ServiceResult<SeatDto>(false, 409, "Seat is already booked.");

        if (seat.Status == "Locked" && seat.LockedByUserId != userId)
            return new ServiceResult<SeatDto>(false, 409, "Seat is currently held by another user.");

        seat.Status = "Locked";
        seat.LockedByUserId = userId;
        seat.LockExpiresAt = DateTime.UtcNow.AddMinutes(5);
        seat.RowVersion = Guid.NewGuid(); // Triggers Optimistic Concurrency check

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ServiceResult<SeatDto>(false, 409, "Concurrency conflict: another user locked this seat first.");
        }

        _cache.Remove($"event_seats_{seat.EventId}");
        var dto = new SeatDto(seat.Id, seat.RowLabel, seat.SeatNumber, seat.Price, seat.Status, seat.LockedByUserId, seat.LockExpiresAt);
        await _hub.Clients.Group($"event-{seat.EventId}").SendAsync("SeatStatusChanged", dto);

        return new ServiceResult<SeatDto>(true, 200, "Seat locked for 5 minutes.", dto);
    }

    public async Task<ServiceResult<Booking>> ConfirmBookingAsync(int seatId, int userId, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return new ServiceResult<Booking>(false, 400, "IdempotencyKey is required.");

        // 1. Idempotency check: prevent double charging on retries
        var existing = await _db.Bookings.FirstOrDefaultAsync(b => b.IdempotencyKey == idempotencyKey);
        if (existing != null)
            return new ServiceResult<Booking>(true, 200, "Duplicate request handled idempotently.", existing);

        // 2. ACID Transaction wrapping wallet deduction + seat booking
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var seat = await _db.Seats.FirstOrDefaultAsync(s => s.Id == seatId);
            if (seat == null)
                return new ServiceResult<Booking>(false, 404, "Seat not found.");

            if (seat.Status != "Locked" || seat.LockedByUserId != userId || seat.LockExpiresAt <= DateTime.UtcNow)
                return new ServiceResult<Booking>(false, 409, "Seat lock expired or not held by you.");

            var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null || wallet.Balance < seat.Price)
                return new ServiceResult<Booking>(false, 400, "Insufficient wallet balance.");

            wallet.Balance -= seat.Price;
            wallet.RowVersion = Guid.NewGuid();

            _db.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Amount = seat.Price,
                Type = "Debit",
                ReferenceKey = idempotencyKey
            });

            seat.Status = "Booked";
            seat.LockedByUserId = null;
            seat.LockExpiresAt = null;
            seat.RowVersion = Guid.NewGuid();

            var booking = new Booking
            {
                UserId = userId,
                SeatId = seat.Id,
                IdempotencyKey = idempotencyKey,
                Status = "Confirmed"
            };
            _db.Bookings.Add(booking);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            _cache.Remove($"event_seats_{seat.EventId}");
            var dto = new SeatDto(seat.Id, seat.RowLabel, seat.SeatNumber, seat.Price, seat.Status, null, null);
            await _hub.Clients.Group($"event-{seat.EventId}").SendAsync("SeatStatusChanged", dto);

            return new ServiceResult<Booking>(true, 200, "Booking confirmed.", booking);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return new ServiceResult<Booking>(false, 409, "Concurrency conflict during payment. Transaction rolled back.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return new ServiceResult<Booking>(false, 500, $"Booking failed and rolled back: {ex.Message}");
        }
    }
}
