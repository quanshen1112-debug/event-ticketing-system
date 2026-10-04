using Microsoft.AspNetCore.SignalR;

namespace EventXpress.Hubs
{
    // Additional Feature: real-time order/sales update notification.
    // Organizers viewing the sales report join a group named after their
    // OrganizerId; OrderController broadcasts to that group whenever a
    // paid order includes one of their events.
    public class NotificationHub : Hub
    {
        public async Task JoinOrganizerGroup(int organizerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"organizer-{organizerId}");
        }
    }
}
