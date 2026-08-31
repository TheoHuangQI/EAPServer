<template>
  <div class="eap-dashboard">
    <section v-if="loadError" class="error-banner">
      <div>
        <strong>Unable to load device information.</strong>
        <span>{{ loadError }}</span>
      </div>
      <el-button size="small" type="primary" :icon="Refresh" @click="reloadDashboard">Retry</el-button>
    </section>

    <section class="dashboard-main">
      <aside class="device-panel">
        <div class="panel-toolbar">
          <el-button text :icon="Refresh" :loading="loading" @click="reloadDashboard" />
        </div>

        <el-input v-model="searchQuery" placeholder="Search equipment" clearable class="device-search">
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>

        <div class="device-list">
          <button
            v-for="item in filteredList"
            :key="item.eqpId"
            type="button"
            class="device-card"
            :class="[
              'device-card--' + getDeviceStatusMeta(item).tone,
              { 'device-card--active': selectedItem === item }
            ]"
            @click="selectItem(item)"
          >
            <span class="device-card__accent"></span>
            <span class="device-card__body">
              <span class="device-card__top">
                <span class="device-card__name">{{ item.eqpId }}</span>
                <span class="status-badge" :class="'status-badge--' + getDeviceStatusMeta(item).tone">
                  <span class="status-dot"></span>
                  {{ getDeviceStatusMeta(item).text }}
                </span>
              </span>
              <span class="device-card__type">{{ item.eqpType || 'N/A' }}</span>
              <span class="device-card__stats">
                <span>
                  <b>{{ getDeviceOnlineLoadPortCount(item) }}</b>
                  / {{ getDeviceLoadPortCount(item) }} LP
                </span>
                <span>
                  <b>{{ getDeviceAlarmCount(item.eqpId) }}</b>
                  Alarm
                </span>
              </span>
              <span class="device-card__states">
                <span class="mini-state" :class="'mini-state--' + getControlStateMeta(item.controlState).tone">
                  {{ getControlStateMeta(item.controlState).text }}
                </span>
                <span class="mini-state" :class="'mini-state--' + getEapStatusMeta(item.eapStatus).tone">
                  EAP {{ getEapStatusMeta(item.eapStatus).text }}
                </span>
              </span>
            </span>
          </button>

          <el-empty v-if="filteredList.length === 0" :image-size="72" description="No devices" />
        </div>
      </aside>

      <main class="work-panel">
        <div class="selected-device-bar">
          <h2>{{ selectedItem ? selectedItem.eqpId : 'No device selected' }}</h2>
          <div v-if="selectedItem" class="selected-states">
            <span class="status-badge" :class="'status-badge--' + getDeviceStatusMeta(selectedItem).tone">
              <span class="status-dot"></span>
              {{ getDeviceStatusMeta(selectedItem).text }}
            </span>
            <span class="status-badge" :class="'status-badge--' + getControlStateMeta(selectedItem.controlState).tone">
              {{ getControlStateMeta(selectedItem.controlState).text }}
            </span>
            <span class="status-badge" :class="'status-badge--' + getEapStatusMeta(selectedItem.eapStatus).tone">
              EAP {{ getEapStatusMeta(selectedItem.eapStatus).text }}
            </span>
          </div>
        </div>

        <div v-if="selectedLpList.length > 0" class="loadport-grid">
          <article v-for="lp in selectedLpList" :key="lp.portId" class="lp-card">
            <header class="lp-card__header">
              <h3>{{ lp.portName || ('LP' + lp.portId) }}</h3>
              <span class="status-badge" :class="'status-badge--' + getLoadPortPrimaryState(lp).tone">
                <span class="status-dot"></span>
                {{ getLoadPortPrimaryState(lp).text }}
              </span>
            </header>

            <div class="lp-card__content">
              <div class="lp-carrier-block">
                <span class="field-label">Carrier</span>
                <strong>{{ formatOptional(lp.carrierId, 'No Carrier') }}</strong>
                <span class="slot-count">{{ getSlotUsage(lp).occupied }} / {{ getSlotUsage(lp).total }} slots occupied</span>
              </div>

              <div class="slot-map" :aria-label="'Slot map for ' + (lp.portName || lp.portId)">
                <div
                  v-for="slot in lp.slots"
                  :key="slot.number"
                  class="slot-cell"
                  :class="'slot-cell--' + getSlotMeta(slot.filled).tone"
                  :title="'Slot ' + formatSlotNumber(slot.number) + ': ' + getSlotMeta(slot.filled).text"
                >
                  <span>{{ formatSlotNumber(slot.number) }}</span>
                </div>
              </div>

              <dl class="lp-state-grid">
                <div>
                  <dt>Lot</dt>
                  <dd>{{ formatOptional(lp.lotId, 'N/A') }}</dd>
                </div>
                <div>
                  <dt>Access Mode</dt>
                  <dd>
                    <span class="mini-state" :class="'mini-state--' + getAccessModeMeta(lp.accessMode).tone">
                      {{ getAccessModeMeta(lp.accessMode).text }}
                    </span>
                  </dd>
                </div>
                <div>
                  <dt>Service State</dt>
                  <dd>
                    <span class="mini-state" :class="'mini-state--' + getTransferStateMeta(lp.transferState).tone">
                      {{ getTransferStateMeta(lp.transferState).text }}
                    </span>
                  </dd>
                </div>
                <div>
                  <dt>Loop Control</dt>
                  <dd>
                    <span class="mini-state" :class="'mini-state--' + getLoopStateMeta(lp.loopState).tone">
                      {{ getLoopStateMeta(lp.loopState).text }}
                    </span>
                  </dd>
                </div>
              </dl>
            </div>

            <footer class="lp-card__actions">
              <div class="action-buttons">
                <el-button
                  size="small"
                  type="primary"
                  :icon="VideoPlay"
                  :disabled="Number(lp.loopState) === 0"
                  @click="handleSetLpLoopState(lp, 0)"
                >
                  Start
                </el-button>
                <el-button
                  size="small"
                  type="warning"
                  :icon="VideoPause"
                  :disabled="Number(lp.loopState) === 2 || Number(lp.loopState) === 1"
                  @click="handleSetLpLoopState(lp, 2)"
                >
                  Pause
                </el-button>
                <el-button
                  size="small"
                  type="danger"
                  :icon="SwitchButton"
                  :disabled="Number(lp.loopState) === 1"
                  @click="handleSetLpLoopState(lp, 1)"
                >
                  Stop
                </el-button>
              </div>
            </footer>
          </article>
        </div>

        <el-empty v-else :image-size="90" description="No LoadPorts for selected device" />

        <section class="alarm-panel">
          <div class="panel-toolbar panel-toolbar--compact">
            <el-button text :icon="Refresh" :loading="alarmLoading" @click="fetchActiveAlarms" />
          </div>

          <div v-if="alarmError" class="alarm-empty alarm-empty--warning">
            Alarm data unavailable: {{ alarmError }}
          </div>
          <div v-else-if="visibleActiveAlarms.length === 0" class="alarm-empty">
            No active exception records.
          </div>
          <div v-else class="alarm-list">
            <button
              v-for="alarm in visibleActiveAlarms"
              :key="alarm.id"
              type="button"
              class="alarm-item"
              @click="openAlarmDetail(alarm)"
            >
              <span class="alarm-item__level">ALARM</span>
              <span class="alarm-item__body">
                <strong>{{ alarm.eqpId || 'N/A' }} {{ alarm.portName || formatPortName(alarm.portId) }}</strong>
                <span>{{ alarm.content || 'No message' }}</span>
              </span>
              <span class="alarm-item__time">{{ alarm.errorTime || 'N/A' }}</span>
            </button>
          </div>
        </section>

        <section class="log-panel">
          <div class="log-toolbar">
            <el-radio-group v-model="logLevelFilter" size="small">
              <el-radio-button label="all">All</el-radio-button>
              <el-radio-button label="info">Info</el-radio-button>
              <el-radio-button label="warning">Warning</el-radio-button>
              <el-radio-button label="error">Error</el-radio-button>
            </el-radio-group>

            <el-select v-model="logDeviceFilter" size="small" class="log-device-filter">
              <el-option label="All Devices" value="all" />
              <el-option
                v-for="device in eqpInfos"
                :key="device.eqpId"
                :label="device.eqpId"
                :value="device.eqpId"
              />
            </el-select>

            <el-input
              v-model="logSearchQuery"
              size="small"
              clearable
              placeholder="Search logs"
              class="log-search"
            >
              <template #prefix>
                <el-icon><Search /></el-icon>
              </template>
            </el-input>

            <div class="log-actions">
              <el-button size="small" :icon="ArrowDown" @click="scrollLogsToBottom">Bottom</el-button>
              <el-button size="small" type="danger" plain :icon="Delete" @click="confirmClearLogs">Clear</el-button>
            </div>
          </div>

          <div ref="logContainer" class="log-container" @scroll="handleLogScroll">
            <div v-if="filteredLogs.length === 0" class="log-empty">No logs match current filters.</div>
            <div
              v-for="log in filteredLogs"
              :key="log.id"
              class="log-item"
              :class="'log-item--' + normalizeLogLevel(log.type)"
            >
              <span class="log-time">{{ log.time }}</span>
              <span class="log-level">{{ normalizeLogLevel(log.type).toUpperCase() }}</span>
              <span class="log-device">{{ log.eqpId }}</span>
              <span class="log-content">{{ log.message }}</span>
            </div>
          </div>
        </section>
      </main>
    </section>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  ArrowDown,
  CircleCheck,
  CircleClose,
  Connection,
  Delete,
  Monitor,
  Refresh,
  Search,
  SwitchButton,
  Timer,
  VideoPause,
  VideoPlay,
  Warning
} from '@element-plus/icons-vue'
import * as signalR from '@microsoft/signalr'
import request from '@/utils/request'
import { GetDashboardSnapshot, GetEqpInfo, GetLoopProcessState, SetLoopProcessState } from '@/api/equipment'
import { GetList as GetExceptionList } from '@/api/Exception'

const router = useRouter()

const LOG_CACHE_LIMIT = 200
const STALE_THRESHOLD_MS = 60000
const SNAPSHOT_POLL_MS = 5000

const loading = ref(false)
const alarmLoading = ref(false)
const searchQuery = ref('')
const eqpInfos = ref([])
const selectedItem = ref(null)
const activeAlarms = ref([])
const loadError = ref('')
const alarmError = ref('')
const logContainer = ref(null)
const logLevelFilter = ref('all')
const logDeviceFilter = ref('all')
const logSearchQuery = ref('')
const isLogPinnedToBottom = ref(true)
const connectionState = ref('connecting')
const lastUpdateAt = ref(null)
const currentTime = ref(Date.now())

let connection = null
let clockTimer = null
let alarmRefreshTimer = null
let snapshotPollTimer = null
let logSequence = 0

const loopStateMap = {
  0: { text: 'Running', tone: 'success' },
  1: { text: 'Stopped', tone: 'danger' },
  2: { text: 'Paused', tone: 'warning' }
}

const transferStateMap = {
  0: { text: 'Out Of Service', tone: 'neutral', online: false },
  1: { text: 'Blocked', tone: 'warning', online: false },
  2: { text: 'Ready To Load', tone: 'info', online: true },
  3: { text: 'Ready To Unload', tone: 'info', online: true },
  4: { text: 'In Service', tone: 'success', online: true },
  5: { text: 'Transfer Ready', tone: 'success', online: true }
}

const accessModeMap = {
  0: { text: 'Manual', tone: 'warning' },
  1: { text: 'Auto', tone: 'success' }
}

const eapStateMap = {
  0: { text: 'Offline', tone: 'danger' },
  1: { text: 'Online', tone: 'success' }
}

const controlStateMap = {
  0: { text: 'Unknown', tone: 'neutral' },
  1: { text: 'EQP Offline', tone: 'danger' },
  2: { text: 'Connecting', tone: 'info' },
  3: { text: 'Host Offline', tone: 'danger' },
  4: { text: 'Local', tone: 'warning' },
  5: { text: 'Remote', tone: 'success' }
}

const namedControlStateMap = {
  UNKNOWN: 0,
  EQPOFFLINE: 1,
  OFFLINEATTEMPTINGTOONLINE: 2,
  HOSTOFFLINE: 3,
  LOCAL: 4,
  ONLOCAL: 4,
  REMOTE: 5,
  ONREMOTE: 5
}

const slotStateMap = {
  0: { text: 'Empty', tone: 'empty', occupied: false },
  1: { text: 'Empty', tone: 'empty', occupied: false },
  3: { text: 'Loaded', tone: 'loaded', occupied: true },
  4: { text: 'Mapped', tone: 'mapped', occupied: true },
  5: { text: 'Processed', tone: 'processed', occupied: true }
}

const filteredList = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return eqpInfos.value
  return eqpInfos.value.filter(item =>
    String(item.eqpId || '').toLowerCase().includes(query) ||
    String(item.eqpType || '').toLowerCase().includes(query)
  )
})

const selectedLpList = computed(() => {
  return selectedItem.value && Array.isArray(selectedItem.value.lpList)
    ? selectedItem.value.lpList
    : []
})

const allLogs = computed(() => {
  return eqpInfos.value.flatMap(eqp =>
    Array.isArray(eqp.logs)
      ? eqp.logs.map(log => ({ ...log, eqpId: eqp.eqpId }))
      : []
  )
})

const filteredLogs = computed(() => {
  const keyword = logSearchQuery.value.trim().toLowerCase()
  return allLogs.value.filter(log => {
    const level = normalizeLogLevel(log.type)
    const matchesLevel = logLevelFilter.value === 'all' || level === logLevelFilter.value
    const matchesDevice = logDeviceFilter.value === 'all' || log.eqpId === logDeviceFilter.value
    const matchesKeyword = !keyword || String(log.message || '').toLowerCase().includes(keyword)
    return matchesLevel && matchesDevice && matchesKeyword
  })
})

const visibleActiveAlarms = computed(() => {
  return activeAlarms.value
})

const alarmHeaderText = computed(() => {
  if (alarmError.value) return 'Data unavailable'
  const count = visibleActiveAlarms.value.length
  return count === 0 ? 'No Active Alarms' : count + ' Active'
})

const overviewStats = computed(() => {
  const totalDevices = eqpInfos.value.length
  const connectedDevices = eqpInfos.value.filter(device => Number(device.eqpStatus) === 1).length
  const totalLoadPorts = eqpInfos.value.reduce((sum, device) => sum + getDeviceLoadPortCount(device), 0)
  const onlineLoadPorts = eqpInfos.value.reduce((sum, device) => {
    return sum + getDeviceOnlineLoadPortCount(device)
  }, 0)
  const activeAlarmAvailable = !alarmError.value
  const activeAlarmCount = activeAlarmAvailable ? activeAlarms.value.length : null

  return {
    totalDevices,
    connectedDevices,
    disconnectedDevices: Math.max(totalDevices - connectedDevices, 0),
    totalLoadPorts,
    onlineLoadPorts,
    activeAlarmLabel: activeAlarmAvailable ? String(activeAlarmCount) : 'N/A',
    activeAlarmClass: !activeAlarmAvailable
      ? 'metric-card--warning'
      : activeAlarmCount > 0
        ? 'metric-card--danger'
        : 'metric-card--success'
  }
})

const isDataStale = computed(() => {
  if (!lastUpdateAt.value) return false
  return currentTime.value - lastUpdateAt.value > STALE_THRESHOLD_MS
})

const communicationMeta = computed(() => {
  if (connectionState.value === 'connected' && isDataStale.value) {
    return { text: 'Data Stale', tone: 'warning' }
  }
  const map = {
    connecting: { text: 'Connecting', tone: 'info' },
    connected: { text: 'Normal', tone: 'success' },
    reconnecting: { text: 'Reconnecting', tone: 'warning' },
    disconnected: { text: 'Disconnected', tone: 'danger' },
    unavailable: { text: 'Unavailable', tone: 'danger' }
  }
  return map[connectionState.value] || { text: 'Unknown', tone: 'neutral' }
})

const lastUpdateClock = computed(() => {
  return lastUpdateAt.value ? formatClock(new Date(lastUpdateAt.value)) : 'Waiting'
})

const lastUpdateText = computed(() => {
  if (!lastUpdateAt.value) return 'waiting'
  const seconds = Math.max(Math.floor((currentTime.value - lastUpdateAt.value) / 1000), 0)
  if (seconds < 2) return 'now'
  if (seconds < 60) return seconds + 's ago'
  return Math.floor(seconds / 60) + 'm ago'
})

function markUpdated() {
  lastUpdateAt.value = Date.now()
  currentTime.value = Date.now()
}

function formatClock(date) {
  return date.toLocaleTimeString('en-GB', { hour12: false })
}

function formatOptional(value, fallback) {
  if (value === null || value === undefined || String(value).trim().length === 0) {
    return fallback
  }
  return String(value)
}

function formatSlotNumber(number) {
  return String(number).padStart(2, '0')
}

function formatPortName(portId) {
  return portId ? 'LP' + portId : 'N/A'
}

function getLoopStateMeta(state) {
  return loopStateMap[Number(state)] || { text: 'Unknown', tone: 'neutral' }
}

function getTransferStateMeta(state) {
  return transferStateMap[Number(state)] || { text: 'Unknown', tone: 'neutral', online: false }
}

function getAccessModeMeta(state) {
  return accessModeMap[Number(state)] || { text: 'Unknown', tone: 'neutral' }
}

function getEapStatusMeta(state) {
  return eapStateMap[Number(state)] || { text: 'Unknown', tone: 'neutral' }
}

function normalizeControlState(controlState) {
  if (typeof controlState === 'string') {
    const normalized = controlState.trim().toUpperCase()
    if (Object.prototype.hasOwnProperty.call(namedControlStateMap, normalized)) {
      return namedControlStateMap[normalized]
    }
  }
  return Number(controlState)
}

function getControlStateMeta(controlState) {
  return controlStateMap[normalizeControlState(controlState)] || controlStateMap[0]
}

function getDeviceStatusMeta(device) {
  const controlMeta = getControlStateMeta(device.controlState)
  if (controlMeta.text === 'Connecting') {
    return controlMeta
  }
  if (Number(device.eqpStatus) === 1) {
    return { text: 'Connected', tone: 'success' }
  }
  if (Number(device.eqpStatus) === 0) {
    return { text: 'Disconnected', tone: 'danger' }
  }
  return { text: 'Unknown', tone: 'neutral' }
}

function getLoadPortPrimaryState(lp) {
  const transferMeta = getTransferStateMeta(lp.transferState)
  if (transferMeta.text === 'Out Of Service' || transferMeta.text === 'Blocked') {
    return transferMeta
  }
  return getLoopStateMeta(lp.loopState)
}

function getSlotMeta(filled) {
  return slotStateMap[Number(filled)] || { text: 'Unknown', tone: 'unknown', occupied: false }
}

function getSlotUsage(lp) {
  const slots = Array.isArray(lp.slots) ? lp.slots : []
  return {
    total: slots.length,
    occupied: slots.filter(slot => getSlotMeta(slot.filled).occupied).length
  }
}

function getDeviceLoadPortCount(device) {
  return Array.isArray(device.lpList) ? device.lpList.length : 0
}

function getDeviceOnlineLoadPortCount(device) {
  if (!Array.isArray(device.lpList)) return 0
  return device.lpList.filter(lp => getTransferStateMeta(lp.transferState).online).length
}

function getDeviceAlarmCount(eqpId) {
  if (alarmError.value) return 'N/A'
  return activeAlarms.value.filter(alarm => alarm.eqpId === eqpId).length
}

function normalizeLogLevel(level) {
  const normalized = String(level || 'info').trim().toLowerCase()
  if (normalized === 'warn') return 'warning'
  if (['info', 'success', 'warning', 'error', 'debug'].includes(normalized)) return normalized
  return 'info'
}

function generateSlots(slotMap, completedSlotMap) {
  const slots = Array.from({ length: 25 }, (_, i) => ({
    filled: 0,
    number: i + 1
  }))
  applySlotMap(slots, slotMap)
  applyCompletedSlots(slots, completedSlotMap)
  return slots
}

function applySlotMap(slots, slotMap) {
  const values = String(slotMap || '').split(';').filter(Boolean)
  if (values.length === slots.length) {
    slots.forEach((slot, index) => {
      slot.filled = Number(values[index])
    })
    return
  }

  values.forEach(slotIndex => {
    const idx = Number(slotIndex) - 1
    if (idx >= 0 && idx < slots.length) {
      slots[idx].filled = 4
    }
  })
}

function applyCompletedSlots(slots, completedSlotMap) {
  String(completedSlotMap || '').split(';').filter(Boolean).forEach(slotIndex => {
    const idx = Number(slotIndex) - 1
    if (idx >= 0 && idx < slots.length) {
      slots[idx].filled = 5
    }
  })
}

function selectItem(item) {
  selectedItem.value = item
}

function applyEquipmentList(list) {
  const previousSelectionId = selectedItem.value && selectedItem.value.eqpId
  const previousLogs = {}
  eqpInfos.value.forEach(eqp => {
    previousLogs[eqp.eqpId] = Array.isArray(eqp.logs) ? eqp.logs : []
  })

  eqpInfos.value = list.map(element => {
    const portInfos = Array.isArray(element.portInfos) ? element.portInfos : []
    portInfos.forEach(port => {
      port.slots = generateSlots(port.slotMap, port.proComSlot)
      port.loopState = port.loopProcessState ?? 0
    })
    return {
      eqpId: element.eqpId,
      eqpType: element.eqpType,
      enable: element.enable,
      eqpStatus: element.eqpStatus,
      eapStatus: element.eapStatus,
      controlState: element.controlState,
      logs: previousLogs[element.eqpId] || [],
      lpList: portInfos
    }
  })
  selectedItem.value = eqpInfos.value.find(item => item.eqpId === previousSelectionId) || eqpInfos.value[0] || null
}

async function reloadDashboard() {
  await fetchDashboardSnapshot()
}

async function fetchDashboardSnapshot(silent = false) {
  if (!silent) {
    loading.value = true
    alarmLoading.value = true
  }
  loadError.value = ''
  alarmError.value = ''
  try {
    const res = await GetDashboardSnapshot()
    if (res && (res.Return === 'NG' || res.return === 'NG')) {
      throw new Error(res.MESSAGE || res.message || 'Dashboard snapshot failed')
    }
    const data = res && res.data ? res.data : {}
    const equipmentList = data.equipments && Array.isArray(data.equipments.list)
      ? data.equipments.list
      : []
    const alarmList = data.activeAlarms && Array.isArray(data.activeAlarms.list)
      ? data.activeAlarms.list
      : []
    applyEquipmentList(equipmentList)
    activeAlarms.value = alarmList
    markUpdated()
  } catch (error) {
    if (!silent) {
      console.error('Failed to get dashboard snapshot, fallback to legacy requests', error)
      await Promise.all([getAllEqpList(), fetchActiveAlarms()])
    } else {
      console.error('Failed to refresh dashboard snapshot', error)
    }
  } finally {
    if (!silent) {
      loading.value = false
      alarmLoading.value = false
    }
  }
}

async function fetchLoopProcessStates() {
  try {
    const res = await GetLoopProcessState()
    const list = res && res.data && Array.isArray(res.data) ? res.data : []
    const stateMap = {}
    list.forEach(item => {
      const key = item.eqpId + '_' + Number(item.portId)
      stateMap[key] = item.loopProcessState
    })
    eqpInfos.value.forEach(eqp => {
      if (Array.isArray(eqp.lpList)) {
        eqp.lpList.forEach(lp => {
          const key = eqp.eqpId + '_' + Number(lp.portId)
          lp.loopState = stateMap[key] ?? lp.loopProcessState ?? 0
        })
      }
    })
    markUpdated()
  } catch (error) {
    console.error('Failed to get loop process states', error)
  }
}

async function handleSetLpLoopState(lp, state) {
  if (!selectedItem.value) return

  const targetState = Number(state)
  if (targetState === 1) {
    try {
      await ElMessageBox.confirm(
        'Stop ' + (lp.portName || formatPortName(lp.portId)) + '? This operation may affect the current job.',
        'Confirm Stop',
        {
          confirmButtonText: 'Confirm Stop',
          cancelButtonText: 'Cancel',
          type: 'warning',
          confirmButtonClass: 'el-button--danger'
        }
      )
    } catch {
      return
    }
  }

  try {
    const res = await SetLoopProcessState(selectedItem.value.eqpId, lp.portId, targetState)
    if (res && (res.Return === 'NG' || res.return === 'NG')) {
      throw new Error(res.MESSAGE || res.message || 'Set loop state failed')
    }
    const newState = res && res.loopProcessState !== undefined ? res.loopProcessState : targetState
    lp.loopState = newState
    markUpdated()
    ElMessage.success('LP ' + (lp.portName || lp.portId) + ' loop state set to ' + getLoopStateMeta(newState).text)
  } catch (error) {
    console.error('Failed to set loop process state', error)
    await fetchLoopProcessStates()
    ElMessage.error('Failed to set loop process state')
  }
}

function appendDeviceLog(eqpInfo, log) {
  eqpInfo.logs.push({
    id: ++logSequence,
    time: log.time || formatClock(new Date()),
    type: normalizeLogLevel(log.msgType),
    message: log.message || '',
    portId: log.portId
  })
  if (eqpInfo.logs.length > LOG_CACHE_LIMIT) {
    eqpInfo.logs.splice(0, eqpInfo.logs.length - LOG_CACHE_LIMIT)
  }
  if (normalizeLogLevel(log.msgType) === 'error') {
    scheduleAlarmRefresh()
  }
}

function updateLoadPort(eqpInfo, portId, updater) {
  if (!Array.isArray(eqpInfo.lpList)) return
  const lpInfo = eqpInfo.lpList.find(m => Number(m.portId) === Number(portId))
  if (lpInfo) updater(lpInfo)
}

function handleReceiveLog(log) {
  const eqpId = log.eqpId
  const eqpInfo = eqpInfos.value.find(a => a.eqpId === eqpId)
  if (!eqpInfo) return

  markUpdated()

  if (log.type === 'Log' || log.type === 'Event') {
    appendDeviceLog(eqpInfo, log)
    if (isLogPinnedToBottom.value) {
      scrollLogsToBottom()
    }
    return
  }

  if (log.type === 'Map') {
    const maps = String(log.message || '').split(';').filter(Boolean)
    updateLoadPort(eqpInfo, log.portId, lpInfo => {
      lpInfo.slotMap = maps.join(';')
      lpInfo.proComSlot = ''
      if (maps.length === lpInfo.slots.length) {
        lpInfo.slots.forEach((slot, index) => {
          slot.filled = Number(maps[index])
        })
      } else {
        lpInfo.slots.forEach(slot => {
          slot.filled = 0
        })
        maps.forEach(slotIndex => {
          const idx = Number(slotIndex) - 1
          if (idx >= 0 && idx < lpInfo.slots.length) {
            lpInfo.slots[idx].filled = 4
          }
        })
      }
    })
    return
  }

  if (log.type === 'SubStrate') {
    updateLoadPort(eqpInfo, log.portId, lpInfo => {
      const idx = Number(log.message) - 1
      if (idx >= 0 && idx < lpInfo.slots.length) {
        lpInfo.slots[idx].filled = 5
      }
      const completedSlots = String(lpInfo.proComSlot || '').split(';').filter(Boolean)
      if (!completedSlots.includes(String(log.message))) {
        completedSlots.push(String(log.message))
      }
      lpInfo.proComSlot = completedSlots.join(';')
    })
    return
  }

  if (log.type === 'Exception') {
    appendDeviceLog(eqpInfo, { ...log, msgType: log.msgType || 'error' })
    scheduleAlarmRefresh()
    return
  }

  if (log.type === 'Alarm' || log.type === 'AlarmClear') {
    appendDeviceLog(eqpInfo, {
      ...log,
      msgType: log.type === 'Alarm' ? 'error' : (log.msgType || 'info')
    })
    scheduleAlarmRefresh()
    if (isLogPinnedToBottom.value) {
      scrollLogsToBottom()
    }
    return
  }

  if (log.type === 'ControlState') {
    eqpInfo.controlState = log.message
  } else if (log.type === 'AccessMode') {
    updateLoadPort(eqpInfo, log.portId, lpInfo => {
      lpInfo.accessMode = log.message
    })
  } else if (log.type === 'TransferState') {
    updateLoadPort(eqpInfo, log.portId, lpInfo => {
      lpInfo.transferState = log.message
    })
  } else if (log.type === 'EapState') {
    eqpInfo.eapStatus = log.message
  } else if (log.type === 'EqpState') {
    eqpInfo.eqpStatus = log.message
  } else if (log.type === 'lot') {
    updateLoadPort(eqpInfo, log.portId, lpInfo => {
      lpInfo.lotId = log.message
    })
  } else if (log.type === 'Carrier') {
    updateLoadPort(eqpInfo, log.portId, lpInfo => {
      lpInfo.carrierId = log.message
    })
  }
}

function getSignalRUrl() {
  const baseURL = request.defaults && request.defaults.baseURL ? request.defaults.baseURL : window.location.origin
  const apiUrl = new URL(baseURL, window.location.origin)
  return new URL('../logHub', apiUrl).href
}

function initSignalR() {
  connectionState.value = 'connecting'
  connection = new signalR.HubConnectionBuilder()
    .withUrl(getSignalRUrl(), {
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets,
      withCredentials: false
    })
    .withAutomaticReconnect([0, 2000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build()

  connection.on('ReceiveLog', handleReceiveLog)
  connection.onreconnecting(() => {
    connectionState.value = 'reconnecting'
  })
  connection.onreconnected(() => {
    connectionState.value = 'connected'
    markUpdated()
    fetchDashboardSnapshot(true)
  })
  connection.onclose(() => {
    connectionState.value = 'disconnected'
    fetchDashboardSnapshot(true)
  })

  connection.start()
    .then(() => {
      connectionState.value = 'connected'
      markUpdated()
    })
    .catch(err => {
      connectionState.value = 'unavailable'
      console.error('SignalR Connection Error: ', err)
    })
}

function startSnapshotPolling() {
  snapshotPollTimer = setInterval(() => {
    if (connectionState.value !== 'connected' || isDataStale.value) {
      fetchDashboardSnapshot(true)
    }
  }, SNAPSHOT_POLL_MS)
}

async function getAllEqpList() {
  loading.value = true
  loadError.value = ''
  try {
    const res = await GetEqpInfo()
    const list = res && res.data && Array.isArray(res.data.list) ? res.data.list : []
    applyEquipmentList(list)
    await fetchLoopProcessStates()
    markUpdated()
  } catch (error) {
    eqpInfos.value = []
    selectedItem.value = null
    loadError.value = error && error.message ? error.message : 'Request failed'
    console.error('Failed to get EAP equipment info', error)
  } finally {
    loading.value = false
  }
}

async function fetchActiveAlarms() {
  alarmLoading.value = true
  alarmError.value = ''
  try {
    const res = await GetExceptionList({ state: 0 })
    const list = res && res.data && Array.isArray(res.data.list) ? res.data.list : []
    activeAlarms.value = list
    markUpdated()
  } catch (error) {
    alarmError.value = error && error.message ? error.message : 'Request failed'
    console.error('Failed to get active alarms', error)
  } finally {
    alarmLoading.value = false
  }
}

function scheduleAlarmRefresh() {
  if (alarmRefreshTimer) return
  alarmRefreshTimer = setTimeout(() => {
    alarmRefreshTimer = null
    fetchActiveAlarms()
  }, 1000)
}

function handleLogScroll() {
  const el = logContainer.value
  if (!el) return
  isLogPinnedToBottom.value = el.scrollHeight - el.scrollTop - el.clientHeight < 24
}

function scrollLogsToBottom() {
  nextTick(() => {
    if (logContainer.value) {
      logContainer.value.scrollTop = logContainer.value.scrollHeight
      isLogPinnedToBottom.value = true
    }
  })
}

async function confirmClearLogs() {
  try {
    await ElMessageBox.confirm(
      'Clear only the frontend real-time log cache currently held by this dashboard?',
      'Clear Log Cache',
      {
        confirmButtonText: 'Clear Cache',
        cancelButtonText: 'Cancel',
        type: 'warning'
      }
    )
  } catch {
    return
  }

  if (logDeviceFilter.value === 'all') {
    eqpInfos.value.forEach(eqp => {
      eqp.logs = []
    })
  } else {
    const target = eqpInfos.value.find(eqp => eqp.eqpId === logDeviceFilter.value)
    if (target) target.logs = []
  }
  ElMessage.success('Frontend log cache cleared')
}

function openAlarmDetail(alarm) {
  router.push({
    path: '/Exception',
    query: {
      eqpId: alarm.eqpId || '',
      state: 0
    }
  })
}

onMounted(async () => {
  clockTimer = setInterval(() => {
    currentTime.value = Date.now()
  }, 15000)
  await reloadDashboard()
  initSignalR()
  startSnapshotPolling()
})

onBeforeUnmount(() => {
  if (clockTimer) {
    clearInterval(clockTimer)
    clockTimer = null
  }
  if (alarmRefreshTimer) {
    clearTimeout(alarmRefreshTimer)
    alarmRefreshTimer = null
  }
  if (snapshotPollTimer) {
    clearInterval(snapshotPollTimer)
    snapshotPollTimer = null
  }
  if (connection) {
    connection.off('ReceiveLog', handleReceiveLog)
    connection.stop().catch(() => {})
    connection = null
  }
})
</script>

<style lang="scss" scoped>
.eap-dashboard {
  min-height: calc(100vh - 64px);
  padding: 14px;
  background: #eef2f6;
  box-sizing: border-box;
  color: #182433;
  text-align: left;
}

.overview-panel,
.device-panel,
.work-panel,
.alarm-panel,
.log-panel {
  border: 1px solid #dce3ec;
  background: #ffffff;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(24, 36, 51, 0.05);
}

.overview-panel {
  padding: 14px;
  margin-bottom: 12px;
}

.overview-title,
.section-head,
.selected-device-bar,
.lp-card__header,
.lp-card__actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.overview-title h1,
.section-head h2,
.selected-device-bar h2,
.lp-card h3 {
  margin: 0;
  color: #152235;
  letter-spacing: 0;
}

.overview-title h1 {
  font-size: 22px;
  line-height: 1.2;
}

.section-head h2,
.selected-device-bar h2 {
  font-size: 16px;
  line-height: 1.25;
}

.lp-card h3 {
  font-size: 16px;
}

.eyebrow {
  margin: 0 0 4px;
  color: #6a7688;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0;
  text-transform: uppercase;
}

.overview-meta,
.selected-states,
.log-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.overview-grid {
  display: grid;
  grid-template-columns: repeat(6, minmax(130px, 1fr));
  gap: 10px;
  margin-top: 14px;
}

.metric-card {
  min-height: 78px;
  padding: 12px;
  border: 1px solid #e0e7ef;
  border-left: 4px solid #8a96a8;
  border-radius: 8px;
  background: #fbfcfe;
  box-sizing: border-box;
}

.metric-card--success {
  border-left-color: #1d8f61;
}

.metric-card--info {
  border-left-color: #2b73d2;
}

.metric-card--warning {
  border-left-color: #c88719;
}

.metric-card--danger {
  border-left-color: #c9372c;
}

.metric-card--neutral {
  border-left-color: #7c8798;
}

.metric-label {
  display: flex;
  align-items: center;
  gap: 6px;
  color: #596679;
  font-size: 12px;
  font-weight: 700;
  line-height: 1.2;
}

.metric-value {
  margin-top: 10px;
  font-size: 24px;
  font-weight: 800;
  color: #162235;
  line-height: 1;
}

.metric-value--text {
  font-size: 18px;
}

.signal-chip,
.last-update,
.status-badge,
.mini-state {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  max-width: 100%;
  min-height: 24px;
  padding: 3px 8px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 700;
  line-height: 1;
  white-space: nowrap;
}

.signal-chip--success,
.status-badge--success,
.mini-state--success {
  color: #13744d;
  background: #e9f7f0;
}

.signal-chip--info,
.status-badge--info,
.mini-state--info {
  color: #1d5fa7;
  background: #e9f2ff;
}

.signal-chip--warning,
.status-badge--warning,
.mini-state--warning {
  color: #8a5a00;
  background: #fff4dc;
}

.signal-chip--danger,
.status-badge--danger,
.mini-state--danger {
  color: #b42318;
  background: #fff0ed;
}

.signal-chip--neutral,
.status-badge--neutral,
.mini-state--neutral {
  color: #5f6b7a;
  background: #f0f3f7;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
}

.last-update {
  color: #5f6b7a;
  background: #f4f7fa;
}

.last-update--stale {
  color: #8a5a00;
  background: #fff4dc;
}

.error-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
  padding: 10px 12px;
  border: 1px solid #ffd0ca;
  border-radius: 8px;
  background: #fff7f5;
  color: #9f261d;
}

.error-banner div {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.dashboard-main {
  display: grid;
  grid-template-columns: minmax(240px, 290px) minmax(0, 1fr);
  gap: 12px;
  align-items: stretch;
}

.panel-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 10px;
}

.panel-toolbar--compact {
  margin-bottom: 12px;
}

.device-panel,
.work-panel {
  min-height: 0;
}

.device-panel {
  display: flex;
  flex-direction: column;
  padding: 12px;
}

.device-search {
  margin-bottom: 10px;
}

.device-search :deep(.el-input__wrapper),
.log-search :deep(.el-input__wrapper) {
  border-radius: 6px;
  box-shadow: 0 0 0 1px #dce4ef inset;
  background: #fbfcfe;
}

.device-list {
  flex: 1;
  min-height: 360px;
  max-height: calc(100vh - 280px);
  overflow-y: auto;
  padding: 2px 2px 8px;
}

.device-card {
  width: 100%;
  min-height: 126px;
  margin-bottom: 10px;
  padding: 0;
  border: 1px solid #e2e9f1;
  border-radius: 8px;
  background: #ffffff;
  color: inherit;
  cursor: pointer;
  text-align: left;
  display: flex;
  overflow: hidden;
  transition: border-color 0.18s ease, box-shadow 0.18s ease, background 0.18s ease;
}

.device-card:hover {
  border-color: #99bee9;
  box-shadow: 0 6px 16px rgba(43, 115, 210, 0.12);
}

.device-card--active {
  border-color: #2b73d2;
  background: #f4f8fe;
  box-shadow: 0 0 0 2px rgba(43, 115, 210, 0.12);
}

.device-card__accent {
  width: 4px;
  background: #8a96a8;
  flex: 0 0 auto;
}

.device-card--success .device-card__accent {
  background: #1d8f61;
}

.device-card--info .device-card__accent {
  background: #2b73d2;
}

.device-card--warning .device-card__accent {
  background: #c88719;
}

.device-card--danger .device-card__accent {
  background: #c9372c;
}

.device-card__body {
  flex: 1;
  min-width: 0;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 9px;
}

.device-card__top,
.device-card__stats,
.device-card__states {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  min-width: 0;
}

.device-card__states {
  justify-content: flex-start;
  flex-wrap: wrap;
}

.device-card__name {
  min-width: 0;
  font-size: 16px;
  font-weight: 800;
  color: #172235;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.device-card__type {
  color: #6a7688;
  font-size: 12px;
  font-weight: 700;
}

.device-card__stats {
  padding: 8px;
  border-radius: 6px;
  background: #f5f7fa;
  color: #5d6878;
  font-size: 12px;
}

.device-card__stats b {
  color: #172235;
  font-size: 15px;
}

.work-panel {
  padding: 12px;
}

.selected-device-bar {
  padding-bottom: 12px;
  border-bottom: 1px solid #e5ebf2;
  margin-bottom: 12px;
}

.loadport-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 12px;
}

.lp-card {
  display: flex;
  flex-direction: column;
  min-height: 430px;
  border: 1px solid #dfe7f0;
  border-radius: 8px;
  background: #fbfcfe;
  overflow: hidden;
}

.lp-card__header {
  padding: 12px;
  border-bottom: 1px solid #e4ebf3;
  background: #ffffff;
}

.lp-card__content {
  flex: 1;
  padding: 12px;
}

.lp-carrier-block {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 4px 12px;
  align-items: end;
  padding-bottom: 10px;
}

.field-label,
.slot-count {
  color: #6a7688;
  font-size: 12px;
  font-weight: 700;
}

.lp-carrier-block strong {
  color: #172235;
  font-size: 20px;
  line-height: 1.15;
  word-break: break-word;
}

.slot-count {
  align-self: end;
  text-align: right;
}

.slot-map {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 6px;
  margin: 8px 0 12px;
}

.slot-cell {
  aspect-ratio: 1.7;
  min-height: 28px;
  border: 1px solid #cfd8e3;
  border-radius: 5px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 800;
  color: #5f6b7a;
  background: #f0f3f7;
}

.slot-cell--loaded {
  color: #145a40;
  border-color: #8ccfb1;
  background: #dff5eb;
}

.slot-cell--mapped {
  color: #4d3b87;
  border-color: #b9abe8;
  background: #eeeafd;
}

.slot-cell--processed {
  color: #145a40;
  border-color: #65bc91;
  background: #c9f0dc;
}

.slot-cell--unknown {
  color: #7a5b00;
  border-color: #f0cb7a;
  background: #fff7df;
}

.lp-state-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  margin: 0;
}

.lp-state-grid div {
  min-height: 60px;
  padding: 8px;
  border: 1px solid #e3e9f1;
  border-radius: 6px;
  background: #ffffff;
  box-sizing: border-box;
}

.lp-state-grid dt {
  margin-bottom: 6px;
  color: #6a7688;
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
}

.lp-state-grid dd {
  margin: 0;
  color: #172235;
  font-size: 13px;
  font-weight: 700;
  word-break: break-word;
}

.lp-card__actions {
  padding: 10px 12px 12px;
  border-top: 1px solid #e2e9f1;
  background: #ffffff;
}

.alarm-panel,
.log-panel {
  margin-top: 12px;
  padding: 12px;
}

.alarm-empty {
  padding: 14px;
  border: 1px dashed #cfd8e3;
  border-radius: 8px;
  color: #526071;
  background: #fbfcfe;
}

.alarm-empty--warning {
  color: #8a5a00;
  background: #fff8e7;
  border-color: #f3cf82;
}

.alarm-list {
  display: grid;
  gap: 8px;
}

.alarm-item {
  width: 100%;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  gap: 10px;
  align-items: center;
  padding: 10px;
  border: 1px solid #ffd0ca;
  border-radius: 8px;
  background: #fff7f5;
  text-align: left;
  cursor: pointer;
}

.alarm-item__level {
  padding: 4px 6px;
  border-radius: 5px;
  color: #b42318;
  background: #ffe7e3;
  font-size: 11px;
  font-weight: 800;
}

.alarm-item__body {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.alarm-item__body strong {
  color: #172235;
}

.alarm-item__body span,
.alarm-item__time {
  color: #69778a;
  font-size: 12px;
}

.alarm-item__time {
  white-space: nowrap;
}

.section-head--logs {
  margin-bottom: 10px;
}

.log-toolbar {
  display: grid;
  grid-template-columns: auto minmax(130px, 190px) minmax(180px, 1fr) auto;
  gap: 10px;
  align-items: center;
  margin-bottom: 10px;
}

.log-device-filter,
.log-search {
  width: 100%;
}

.action-buttons {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  width: 100%;
}

.action-buttons :deep(.el-button) {
  width: 100%;
  min-width: 0;
}

.log-container {
  height: 280px;
  overflow-y: auto;
  border: 1px solid #202b39;
  border-radius: 8px;
  background: #121923;
  color: #dbe4ef;
  font-family: Consolas, Monaco, monospace;
}

.log-empty {
  padding: 18px;
  color: #7f8ea3;
}

.log-item {
  display: grid;
  grid-template-columns: 76px 74px 110px minmax(0, 1fr);
  gap: 8px;
  align-items: start;
  padding: 7px 10px;
  border-bottom: 1px solid rgba(219, 228, 239, 0.07);
  font-size: 12px;
  line-height: 1.45;
}

.log-time {
  color: #8fa0b5;
}

.log-level {
  font-weight: 800;
}

.log-device {
  color: #c5d1e0;
  font-weight: 700;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.log-content {
  min-width: 0;
  color: #edf4fc;
  word-break: break-word;
}

.log-item--info .log-level {
  color: #8fbdf5;
}

.log-item--success .log-level {
  color: #76d69f;
}

.log-item--warning .log-level {
  color: #efbd5d;
}

.log-item--error .log-level {
  color: #ff8c80;
}

.log-item--debug .log-level {
  color: #a9b5c4;
}

.device-list::-webkit-scrollbar,
.log-container::-webkit-scrollbar {
  width: 6px;
}

.device-list::-webkit-scrollbar-thumb,
.log-container::-webkit-scrollbar-thumb {
  background: #aeb9c8;
  border-radius: 4px;
}

@media (max-width: 1280px) {
  .overview-grid {
    grid-template-columns: repeat(3, minmax(150px, 1fr));
  }

  .dashboard-main {
    grid-template-columns: 260px minmax(0, 1fr);
  }
}

@media (max-width: 960px) {
  .dashboard-main {
    grid-template-columns: 1fr;
  }

  .device-list {
    max-height: 320px;
  }

  .log-toolbar {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .selected-device-bar,
  .alarm-item {
    align-items: flex-start;
    grid-template-columns: 1fr;
    flex-direction: column;
  }

  .lp-state-grid {
    grid-template-columns: 1fr;
  }

  .log-item {
    grid-template-columns: 70px 68px minmax(0, 1fr);
  }

  .log-device {
    display: none;
  }

  .log-toolbar {
    grid-template-columns: 1fr;
  }

  .action-buttons {
    grid-template-columns: 1fr;
  }
}
</style>
