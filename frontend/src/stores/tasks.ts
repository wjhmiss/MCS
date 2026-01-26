import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { TaskDefinition, TaskExecution } from '@/types'
import { tasksApi } from '@/services/api'

export const useTaskStore = defineStore('tasks', () => {
  const tasks = ref<TaskDefinition[]>([])
  const executions = ref<TaskExecution[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const activeTasks = computed(() =>
    tasks.value.filter(t => t.isEnabled)
  )

  const runningExecutions = computed(() =>
    executions.value.filter(e => e.status === 'Running')
  )

  async function executeTask(taskId: string, inputData: Record<string, any>) {
    loading.value = true
    error.value = null
    try {
      const response = await tasksApi.execute(taskId, inputData)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function stopTask(taskId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await tasksApi.stop(taskId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function pauseTask(taskId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await tasksApi.pause(taskId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function resumeTask(taskId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await tasksApi.resume(taskId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function getTaskStatus(taskId: string) {
    loading.value = true
    error.value = null
    try {
      const response = await tasksApi.getStatus(taskId)
      return response
    } catch (err: any) {
      error.value = err.message
      throw err
    } finally {
      loading.value = false
    }
  }

  async function updateTaskConfig(taskId: string, config: Record<string, any>) {
    loading.value = true
    error.value = null
    try {
      const response = await tasksApi.updateConfig(taskId, config)
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
    tasks,
    executions,
    loading,
    error,
    activeTasks,
    runningExecutions,
    executeTask,
    stopTask,
    pauseTask,
    resumeTask,
    getTaskStatus,
    updateTaskConfig,
    updateExecutionStatus
  }
})
