<template>
  <div id="app">
    <el-container style="height: 100vh">
      <el-aside width="200px" style="background-color: #304156">
        <div class="logo">
          <h3>MCS 任务管理</h3>
        </div>
        <el-menu
          :default-active="activeMenu"
          background-color="#304156"
          text-color="#bfcbd9"
          active-text-color="#409eff"
          @select="handleMenuSelect"
        >
          <el-menu-item index="dashboard">
            <el-icon><Odometer /></el-icon>
            <span>仪表盘</span>
          </el-menu-item>
          <el-menu-item index="tasks">
            <el-icon><List /></el-icon>
            <span>任务组件库</span>
          </el-menu-item>
          <el-menu-item index="workflows">
            <el-icon><Connection /></el-icon>
            <span>工作流编辑</span>
          </el-menu-item>
          <el-menu-item index="executions">
            <el-icon><Timer /></el-icon>
            <span>执行历史</span>
          </el-menu-item>
          <el-menu-item index="schedules">
            <el-icon><Clock /></el-icon>
            <span>定时任务</span>
          </el-menu-item>
          <el-menu-item index="mqtt">
            <el-icon><Message /></el-icon>
            <span>MQTT管理</span>
          </el-menu-item>
          <el-menu-item index="alerts">
            <el-icon><Bell /></el-icon>
            <span>告警管理</span>
          </el-menu-item>
        </el-menu>
      </el-aside>
      <el-container>
        <el-header>
          <div class="header-content">
            <span>{{ currentTitle }}</span>
            <div class="header-actions">
              <el-badge :value="alertCount" class="item">
                <el-button :icon="Bell" circle @click="activeMenu = 'alerts'" />
              </el-badge>
              <el-dropdown>
                <span class="el-dropdown-link">
                  管理员
                  <el-icon class="el-icon--right"><arrow-down /></el-icon>
                </span>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item>个人设置</el-dropdown-item>
                    <el-dropdown-item>退出登录</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </div>
          </div>
        </el-header>
        <el-main>
          <component :is="currentComponent" />
        </el-main>
      </el-container>
    </el-container>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Odometer,
  List,
  Connection,
  Timer,
  Clock,
  Message,
  Bell,
  ArrowDown
} from '@element-plus/icons-vue'
import Dashboard from './components/Dashboard.vue'
import TaskList from './components/TaskList.vue'
import WorkflowEditor from './components/WorkflowEditor.vue'
import WorkflowExecution from './components/WorkflowExecution.vue'
import ScheduleManagement from './components/ScheduleManagement.vue'
import MQTTManagement from './components/MQTTManagement.vue'
import AlertManagement from './components/AlertManagement.vue'

const activeMenu = ref('dashboard')
const alertCount = ref(3)

const currentTitle = computed(() => {
  const titles: Record<string, string> = {
    dashboard: '仪表盘',
    tasks: '任务组件库',
    workflows: '工作流编辑',
    executions: '执行历史',
    schedules: '定时任务',
    mqtt: 'MQTT管理',
    alerts: '告警管理'
  }
  return titles[activeMenu.value] || 'MCS 任务管理系统'
})

const currentComponent = computed(() => {
  const components: Record<string, any> = {
    dashboard: Dashboard,
    tasks: TaskList,
    workflows: WorkflowEditor,
    executions: WorkflowExecution,
    schedules: ScheduleManagement,
    mqtt: MQTTManagement,
    alerts: AlertManagement
  }
  return components[activeMenu.value] || Dashboard
})

function handleMenuSelect(index: string) {
  activeMenu.value = index
}
</script>

<script lang="ts">
export default {
  name: 'App'
}
</script>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

#app {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

.logo {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  border-bottom: 1px solid #1f2d3d;
}

.logo h3 {
  margin: 0;
  font-size: 18px;
}

.el-header {
  background-color: #fff;
  border-bottom: 1px solid #e6e6e6;
  display: flex;
  align-items: center;
  padding: 0 20px;
  height: 60px;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 20px;
}

.el-dropdown-link {
  cursor: pointer;
  color: #606266;
}

.el-main {
  background-color: #f0f2f5;
  padding: 20px;
}

.el-aside {
  overflow-x: hidden;
}

.el-menu {
  border-right: none;
}
</style>
