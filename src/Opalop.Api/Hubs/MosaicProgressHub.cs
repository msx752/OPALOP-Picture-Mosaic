namespace Opalop.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

public class MosaicProgressHub : Hub
{
    public Task SubscribeToJob(string jobId)
        => Groups.AddToGroupAsync(Context.ConnectionId, jobId);

    public Task UnsubscribeFromJob(string jobId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
}
