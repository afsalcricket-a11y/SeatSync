using Microsoft.AspNetCore.SignalR;

namespace SeatSync.API.Hubs;

public class SeatHub : Hub
{
    public async Task JoinEventRoom(int eventId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"event-{eventId}");
    }
}