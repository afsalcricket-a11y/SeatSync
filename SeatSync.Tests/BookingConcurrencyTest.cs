using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SeatSync.API.Data;
using SeatSync.API.Hubs;
using SeatSync.API.Models;
using SeatSync.API.Services;

namespace SeatSync.Tests;

public class BookingConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public BookingConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();

        db.Users.AddRange(
            new User { Id = 1, FullName = "User A", Email = "a@test.com", Wallet = new Wallet { Id = 1, UserId = 1, Balance = 5000m } },
            new User { Id = 2, FullName = "User B", Email = "b@test.com", Wallet = new Wallet { Id = 2, UserId = 2, Balance = 500m } }
        );
        db.Events.Add(new Event { Id = 1, Title = "IPL Final", Venue = "Chepauk", EventDate = DateTime.UtcNow.AddDays(10) });
        db.Seats.Add(new Seat { Id = 1, EventId = 1, RowLabel = "A", SeatNumber = 1, Price = 1500m });
        db.SaveChanges();
    }

    private BookingService CreateService(AppDbContext db)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var hub = new FakeHubContext();
        return new BookingService(db, cache, hub);
    }

    [Fact]
    public async Task LockSeat_WhenAlreadyLockedByAnotherUser_Returns409Conflict()
    {
        using var db1 = new AppDbContext(_options);
        using var db2 = new AppDbContext(_options);

        var res1 = await CreateService(db1).LockSeatAsync(seatId: 1, userId: 1);
        var res2 = await CreateService(db2).LockSeatAsync(seatId: 1, userId: 2);

        Assert.True(res1.Success);
        Assert.Equal(200, res1.StatusCode);
        Assert.False(res2.Success);
        Assert.Equal(409, res2.StatusCode);
    }

    [Fact]
    public async Task ConfirmBooking_WithInsufficientBalance_RollsBackAndKeepsSeatLocked()
    {
        using var db = new AppDbContext(_options);
        var service = CreateService(db);

        await service.LockSeatAsync(seatId: 1, userId: 2); // User B has ₹500; seat costs ₹1500
        var confirmRes = await service.ConfirmBookingAsync(seatId: 1, userId: 2, idempotencyKey: "idem-low-bal-1");

        Assert.False(confirmRes.Success);
        Assert.Equal(400, confirmRes.StatusCode);

        var wallet = await db.Wallets.FirstAsync(w => w.UserId == 2);
        Assert.Equal(500m, wallet.Balance);
    }

    [Fact]
    public async Task ConfirmBooking_DuplicateIdempotencyKey_DoesNotDoubleCharge()
    {
        using var db = new AppDbContext(_options);
        var service = CreateService(db);

        await service.LockSeatAsync(seatId: 1, userId: 1);
        var first = await service.ConfirmBookingAsync(seatId: 1, userId: 1, idempotencyKey: "idem-retry-99");
        var second = await service.ConfirmBookingAsync(seatId: 1, userId: 1, idempotencyKey: "idem-retry-99");

        Assert.True(first.Success);
        Assert.True(second.Success);

        var wallet = await db.Wallets.FirstAsync(w => w.UserId == 1);
        Assert.Equal(3500m, wallet.Balance); // Charged ₹1500 only once
        Assert.Equal(1, await db.Bookings.CountAsync());
    }

    public void Dispose() => _connection.Dispose();

    private class FakeHubContext : IHubContext<SeatHub>
    {
        public IHubClients Clients => new FakeHubClients();
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private class FakeHubClients : IHubClients
    {
        public IClientProxy All => new FakeClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Client(string connectionId) => new FakeClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public IClientProxy Group(string groupName) => new FakeClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public IClientProxy User(string userId) => new FakeClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
    }

    private class FakeClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}