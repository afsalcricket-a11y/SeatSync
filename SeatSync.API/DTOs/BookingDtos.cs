namespace SeatSync.API.DTOs;

    public record RegisterRequest(string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, int UserId, string FullName, decimal WalletBalance);

public record LockSeatRequest(int SeatId);
public record ConfirmBookingRequest(int SeatId, string IdempotencyKey);

public record SeatDto(
    int Id,
    string RowLabel,
    int SeatNumber,
    decimal Price,
    string Status,
    int? LockedByUserId,
    DateTime? LockExpiresAt
);

public record ServiceResult<T>(bool Success, int StatusCode, string Message, T? Data = default);