using Microsoft.AspNetCore.SignalR;

namespace MCS.Hubs
{
    public class TaskHub : Hub
    {
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task NotifyTaskUpdate(string taskId, string status)
        {
            await Clients.Group("tasks").SendAsync("TaskUpdated", taskId, status);
        }

        public async Task NotifyWorkflowUpdate(string workflowId, string status)
        {
            await Clients.Group("workflows").SendAsync("WorkflowUpdated", workflowId, status);
        }
    }
}
