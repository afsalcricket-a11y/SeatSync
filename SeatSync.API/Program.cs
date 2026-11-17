using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SeatSync.API.Data;
using SeatSync.API.Hubs;
using SeatSync.API.Models;
using SeatSync.API.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddScoped<BookingService>();
builder.Services.AddControllers();

// Sliding-Window Rate Limiter (blocks scalping bots with 429 Too Many Requests)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddSlidingWindowLimiter("booking-limit", opt =>
    {
        opt.PermitLimit = 15;
        opt.Window = TimeSpan.FromSeconds(10);
        opt.SegmentsPerWindow = 2;
    });
});

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// .NET 10 Native OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-create SQLite database & seed 1 IPL Match Event with 20 seats on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Events.Any())
    {
        var ev = new Event
        {
            Title = "IPL 2027 Final — CSK vs RCB",
            Venue = "M. A. Chidambaram Stadium, Chennai",
            EventDate = DateTime.UtcNow.AddDays(30)
        };
        db.Events.Add(ev);
        db.SaveChanges();

        var seats = new List<Seat>();
        for (int i = 1; i <= 10; i++)
        {
            seats.Add(new Seat { EventId = ev.Id, RowLabel = "A", SeatNumber = i, Price = 1500m });
            seats.Add(new Seat { EventId = ev.Id, RowLabel = "B", SeatNumber = i, Price = 1000m });
        }
        db.Seats.AddRange(seats);
        db.SaveChanges();
    }
}

app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SeatHub>("/hubs/seats");

app.Run();


