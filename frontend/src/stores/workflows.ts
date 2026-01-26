import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { WorkflowDefinition, WorkflowNode, WorkflowConnection, WorkflowExecution } from '@/types'
import { workflowsApi } from '@/services/api'

export const useWorkflowStore = defineStore('workflows', () => {
  const workflows = ref<WorkflowDefinition[]>([])
  const nodes = ref<WorkflowNode[]>([])
  const connections = ref<WorkflowConnection[]>([])
  const executions = ref<WorkflowExecution[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const activeWorkflows = computed(() =>
    workflows.value.filter(w => w.isEnabled)
  )

  const runningExecutions = computed(() =>
    executions.value.filter(e => e.status === 'Running')
  )

  async function startWorkflow(workflowId: string, inputData: Record<string, any>) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.start(workflowId, inputData)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function stopWorkflow(workflowId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.stop(workflowId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function pauseWorkflow(workflowId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.pause(workflowId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function resumeWorkflow(workflowId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.resume(workflowId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function getWorkflowStatus(workflowId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.getStatus(workflowId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function addNode(workflowId: string, node: WorkflowNode) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.addNode(workflowId, node)
      nodes.value.push(node)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function removeNode(workflowId: string, nodeId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.removeNode(workflowId, nodeId)
      nodes.value = nodes.value.filter(n => n.id !== nodeId)
      connections.value = connections.value.filter(c => c.fromNodeId !== nodeId && c.toNodeId !== nodeId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function addConnection(workflowId: string, connection: WorkflowConnection) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.addConnection(workflowId, connection)
      connections.value.push(connection)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function removeConnection(workflowId: string, connectionId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.removeConnection(workflowId, connectionId)
      connections.value = connections.value.filter(c => c.id !== connectionId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function skipNode(workflowId: string, nodeId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.skipNode(workflowId, nodeId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function terminateWorkflow(workflowId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await workflowsApi.terminate(workflowId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  function updateExecutionStatus(executionId: string, status: string) {
    const execution = executions.value.find(e => e.id === executionId)
    if (execution) {
      execution.status = status
    }
  }

  return {
    workflows,
    nodes,
    connections,
    executions,
    loading,
    error,
    activeWorkflows,
    runningExecutions,
    startWorkflow,
    stopWorkflow,
    pauseWorkflow,
    resumeWorkflow,
    getWorkflowStatus,
    addNode,
    removeNode,
    addConnection,
    removeConnection,
    skipNode,
    terminateWorkflow,
    updateExecutionStatus
  }
})
