import * as signalR from '@microsoft/signalr'

class SignalRService {
  private connection: signalR.HubConnection | null = null
  private reconnectAttempts = 0
  private maxReconnectAttempts = 5

  async connect(hubUrl: string = '/hubs/tasks'): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.previousRetryCount < this.maxReconnectAttempts) {
            return 1000 * Math.pow(2, retryContext.previousRetryCount)
          }
          return null
        }
      })
      .build()

    this.connection.onreconnecting((error) => {
      console.log('SignalR reconnecting...', error)
      this.reconnectAttempts++
    })

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected with connectionId:', connectionId)
      this.reconnectAttempts = 0
    })

    this.connection.onclose((error) => {
      console.log('SignalR connection closed', error)
    })

    try {
      await this.connection.start()
      console.log('SignalR connected')
    } catch (error) {
      console.error('SignalR connection error:', error)
      throw error
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop()
      this.connection = null
    }
  }

  onTaskUpdated(callback: (taskId: string, status: string) => void): void {
    this.connection?.on('TaskUpdated', callback)
  }

  onWorkflowUpdated(callback: (workflowId: string, status: string) => void): void {
    this.connection?.on('WorkflowUpdated', callback)
  }

  offTaskUpdated(callback: (taskId: string, status: string) => void): void {
    this.connection?.off('TaskUpdated', callback)
  }

  offWorkflowUpdated(callback: (workflowId: string, status: string) => void): void {
    this.connection?.off('WorkflowUpdated', callback)
  }

  async joinGroup(groupName: string): Promise<void> {
    await this.connection?.invoke('JoinGroup', groupName)
  }

  async leaveGroup(groupName: string): Promise<void> {
    await this.connection?.invoke('LeaveGroup', groupName)
  }

  getConnectionState(): signalR.HubConnectionState {
    return this.connection?.state ?? signalR.HubConnectionState.Disconnected
  }
}

export default new SignalRService()
