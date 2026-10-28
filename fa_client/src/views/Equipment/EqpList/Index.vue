<template>
  <div class="page-shell">
    <section class="filter-panel">
      <div class="filter-grid">
        <el-input
          v-model="pageInfo.eqpId"
          placeholder="设备ID"
          clearable
          @keyup.enter="handleSearch"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>

        <el-input
          v-model="pageInfo.eqpType"
          placeholder="设备类型"
          clearable
          @keyup.enter="handleSearch"
        >
          <template #prefix>
            <el-icon><Monitor /></el-icon>
          </template>
        </el-input>

        <div class="toolbar">
          <el-button type="primary" @click="handleSearch">
            <el-icon><Search /></el-icon>
            查询
          </el-button>
          <el-button plain @click="handleReset">
            <el-icon><RefreshLeft /></el-icon>
            重置
          </el-button>
          <el-button plain :loading="loading" @click="fetchData">
            <el-icon><Refresh /></el-icon>
            刷新
          </el-button>
          <el-button type="success" @click="handleAdd">
            <el-icon><Plus /></el-icon>
            新增
          </el-button>
        </div>
      </div>
    </section>

    <section class="summary-grid">
      <article class="metric-card metric-card--info">
        <span>总设备数</span>
        <strong>{{ stats.total }}</strong>
      </article>
      <article class="metric-card metric-card--success">
        <span>在线设备</span>
        <strong>{{ stats.online }}</strong>
      </article>
      <article class="metric-card metric-card--danger">
        <span>离线设备</span>
        <strong>{{ stats.offline }}</strong>
      </article>
      <article class="metric-card metric-card--neutral">
        <span>已启用</span>
        <strong>{{ stats.enabled }}</strong>
      </article>
    </section>

    <section class="table-panel">
      <div class="table-toolbar">
        <div class="selection-text">
          已选择 <b>{{ selectedDevices.length }}</b> 台设备
        </div>
        <div class="toolbar toolbar--right">
          <el-button size="small" plain :disabled="selectedDevices.length === 0" @click="handleBatchEnable(1)">
            <el-icon><CircleCheck /></el-icon>
            批量启用
          </el-button>
          <el-button size="small" plain :disabled="selectedDevices.length === 0" @click="handleBatchEnable(0)">
            <el-icon><CircleClose /></el-icon>
            批量禁用
          </el-button>
          <el-button size="small" type="danger" plain :disabled="selectedDevices.length === 0" @click="handleBatchDelete">
            <el-icon><Delete /></el-icon>
            批量删除
          </el-button>
        </div>
      </div>

      <el-table
        v-loading="loading"
        :data="paginatedData"
        border
        stripe
        highlight-current-row
        row-key="eqpId"
        max-height="calc(100vh - 360px)"
        @selection-change="handleSelectionChange"
        @sort-change="handleSortChange"
      >
        <el-table-column type="selection" width="54" align="center" reserve-selection fixed />
        <el-table-column :index="indexHandler" label="序号" type="index" width="74" align="center" fixed sortable="custom" />
        <el-table-column prop="eqpId" label="设备号" min-width="150" fixed sortable="custom" show-overflow-tooltip />
        <el-table-column prop="eqpType" label="设备类型" min-width="150" sortable="custom" show-overflow-tooltip />
        <el-table-column label="连接配置" min-width="230">
          <template #default="scope">
            <div v-if="scope.row.connectionConfig" class="connection-summary">
              <el-tag size="small" effect="plain">{{ scope.row.connectionConfig.connectionType }}</el-tag>
              <el-tag size="small" effect="plain" type="info">{{ scope.row.connectionConfig.connectionMode }}</el-tag>
              <span>{{ connectionSummary(scope.row.connectionConfig) }}</span>
            </div>
            <el-tag v-else size="small" :type="scope.row.connectionConfigError ? 'danger' : 'warning'" effect="plain">
              {{ scope.row.connectionConfigError ? '读取失败' : '未配置' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="enable" label="启用" width="110" align="center" sortable="custom">
          <template #default="scope">
            <el-switch
              :model-value="isEnabled(scope.row.enable)"
              active-text="启用"
              inactive-text="禁用"
              inline-prompt
              :loading="scope.row.__switching"
              @change="value => handleEnableChange(scope.row, value)"
            />
          </template>
        </el-table-column>
        <el-table-column prop="eqpStatus" label="设备状态" width="150" align="center" sortable="custom">
          <template #default="scope">
            <span class="status-badge" :class="'status-badge--' + statusMeta(scope.row.eqpStatus).tone">
              <span class="status-dot"></span>
              {{ statusMeta(scope.row.eqpStatus).text }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="eapStatus" label="EAP状态" width="150" align="center" sortable="custom">
          <template #default="scope">
            <span class="status-badge" :class="'status-badge--' + statusMeta(scope.row.eapStatus).tone">
              <span class="status-dot"></span>
              {{ statusMeta(scope.row.eapStatus).text }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="210" align="center" fixed="right">
          <template #default="scope">
            <div class="icon-actions">
              <el-tooltip content="连接参数" placement="top">
                <el-button type="primary" plain size="small" @click="handleConnectionConfig(scope.row)">
                  参数
                </el-button>
              </el-tooltip>
              <el-tooltip content="编辑" placement="top">
                <el-button type="primary" circle size="small" @click="handleEdit(scope.row)">
                  <el-icon><Edit /></el-icon>
                </el-button>
              </el-tooltip>
              <el-tooltip content="删除" placement="top">
                <el-button type="danger" circle size="small" @click="handleDelete(scope.row)">
                  <el-icon><Delete /></el-icon>
                </el-button>
              </el-tooltip>
              <el-dropdown trigger="click" @command="command => handleMoreCommand(command, scope.row)">
                <el-button circle size="small">
                  <el-icon><MoreFilled /></el-icon>
                </el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="refresh">刷新列表</el-dropdown-item>
                    <el-dropdown-item command="copy">复制设备ID</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </div>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination-bar">
        <el-pagination
          v-model:current-page="pageInfo.pageNo"
          v-model:page-size="pageInfo.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="sortedData.length"
          background
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange"
          @current-change="handlePageChange"
        />
      </div>
    </section>

    <EquipmentDialog v-model:visible="showModal" :is-edit="isEdit" :form="formData" @submit="handleDialogSubmit" />

    <el-dialog
      v-model="showConfigModal"
      title="设备连接配置"
      width="min(860px, calc(100vw - 32px))"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <el-form :model="configForm" label-width="145px" class="connection-config-form">
        <el-row :gutter="16">
          <el-col :xs="24" :sm="12">
            <el-form-item label="设备名称">
              <el-input v-model="configForm.name" disabled />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="Device ID">
              <el-input-number v-model="configForm.deviceID" :min="0" :max="65535" controls-position="right" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="连接模式">
              <el-select v-model="configForm.connectionMode" style="width: 100%">
                <el-option label="ACTIVE（主动）" value="ACTIVE" />
                <el-option label="PASSIVE（被动）" value="PASSIVE" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="连接类型">
              <el-select v-model="configForm.connectionType" style="width: 100%">
                <el-option label="HSMS" value="HSMS" />
                <el-option label="SECSI" value="SECSI" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="本地 IP">
              <el-input v-model="configForm.localIP" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="本地端口">
              <el-input-number v-model="configForm.localPort" :min="1" :max="65535" controls-position="right" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="远端 IP">
              <el-input v-model="configForm.remoteIP" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="远端端口">
              <el-input-number v-model="configForm.remotePort" :min="0" :max="65535" controls-position="right" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="设备类型">
              <el-input v-model="configForm.eqpType" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12">
            <el-form-item label="LoadPort 数量">
              <el-input-number v-model="configForm.loadPort" :min="1" controls-position="right" />
            </el-form-item>
          </el-col>
          <el-col v-for="field in timeoutFields" :key="field.key" :xs="12" :sm="8">
            <el-form-item :label="field.label">
              <el-input-number v-model="configForm[field.key]" :min="1" controls-position="right" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="Chamber">
              <el-input v-model="configForm.chamber" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="Event Report 文件">
              <el-input v-model="configForm.eventReportFile" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="自动重连">
              <el-switch v-model="configForm.autoReconnect" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="showConfigModal = false">取消</el-button>
        <el-button type="primary" :loading="configSaving" @click="saveConnectionConfig">保存配置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import * as signalR from '@microsoft/signalr'
import { GetList, Add, Edit, Delete, Deletes, GetConnectionConfig, SetConnectionConfig } from '@/api/equipment'
import request from '@/utils/request'
import EquipmentDialog from './Dialog.vue'

export default {
  name: 'EquipmentListPage',
  components: { EquipmentDialog },
  data() {
    return {
      loading: false,
      selectedDevices: [],
      showModal: false,
      showConfigModal: false,
      configSaving: false,
      configDeviceId: '',
      connectionConfigs: {},
      configForm: this.getDefaultConnectionConfig(),
      timeoutFields: [
        { key: 't3', label: 'T3 超时（秒）' },
        { key: 't5', label: 'T5 超时（秒）' },
        { key: 't6', label: 'T6 超时（秒）' },
        { key: 't7', label: 'T7 超时（秒）' },
        { key: 't8', label: 'T8 超时（秒）' },
        { key: 'heartbeatInterval', label: '心跳周期（秒）' },
        { key: 'heartbeatMaxFailures', label: '心跳失败次数' }
      ],
      isEdit: false,
      tableData: [],
      formData: this.getDefaultForm(),
      total: 0,
      connection: null,
      statusRefreshTimer: null,
      statusRefreshInFlight: false,
      sortInfo: {
        prop: '',
        order: ''
      },
      pageInfo: {
        eqpId: '',
        eqpType: '',
        pageNo: 1,
        pageSize: 10
      }
    }
  },
  computed: {
    sortedData() {
      const data = [...this.tableData]
      if (!this.sortInfo.prop || !this.sortInfo.order) {
        return data
      }

      const direction = this.sortInfo.order === 'ascending' ? 1 : -1
      return data.sort((a, b) => {
        const left = a[this.sortInfo.prop]
        const right = b[this.sortInfo.prop]
        if (left === right) return 0
        if (left === null || left === undefined) return -1 * direction
        if (right === null || right === undefined) return 1 * direction
        return String(left).localeCompare(String(right), 'zh-CN', { numeric: true }) * direction
      })
    },
    paginatedData() {
      const start = (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize
      const end = this.pageInfo.pageNo * this.pageInfo.pageSize
      return this.sortedData.slice(start, end)
    },
    stats() {
      const total = this.tableData.length || this.total
      const online = this.tableData.filter(item => this.isConnected(item.eqpStatus)).length
      const enabled = this.tableData.filter(item => this.isEnabled(item.enable)).length

      return {
        total,
        online,
        offline: Math.max(total - online, 0),
        enabled
      }
    }
  },
  created() {
    this.fetchData()
  },
  mounted() {
    this.initSignalR()
    this.startStatusPolling()
  },
  beforeUnmount() {
    this.stopStatusPolling()
    this.stopSignalR()
  },
  methods: {
    getDefaultForm() {
      return {
        eqpId: '',
        eqpType: '',
        enable: 1
      }
    },
    getDefaultConnectionConfig() {
      return {
        name: '',
        deviceID: 0,
        connectionMode: 'ACTIVE',
        localIP: '0.0.0.0',
        localPort: 5000,
        remoteIP: '0.0.0.0',
        remotePort: 5000,
        eqpType: '',
        t3: 45,
        t5: 10,
        t6: 5,
        t7: 10,
        t8: 5,
        connectionType: 'HSMS',
        chamber: '9,10,11,13,14,15,17,18,19,21,22,23',
        loadPort: 4,
        eventReportFile: 'event-report-CH.xml',
        heartbeatInterval: 30,
        heartbeatMaxFailures: 3,
        autoReconnect: true
      }
    },
    mapConnectionConfig(config) {
      return {
        name: config.Name,
        deviceID: Number(config.DeviceID),
        connectionMode: config.ConnectionMode,
        localIP: config.LocalIP,
        localPort: Number(config.LocalPort),
        remoteIP: config.RemoteIP,
        remotePort: Number(config.RemotePort),
        eqpType: config.EQPType,
        t3: Number(config.T3),
        t5: Number(config.T5),
        t6: Number(config.T6),
        t7: Number(config.T7),
        t8: Number(config.T8),
        connectionType: config.ConnectionType,
        chamber: config.Chamber,
        loadPort: Number(config.LoadPort),
        eventReportFile: config.EventReportFile,
        heartbeatInterval: Number(config.HeartbeatInterval),
        heartbeatMaxFailures: Number(config.HeartbeatMaxFailures),
        autoReconnect: config.AutoReconnect !== false
      }
    },
    serializeConnectionConfig(config) {
      return {
        Name: config.name,
        DeviceID: Number(config.deviceID),
        ConnectionMode: config.connectionMode,
        LocalIP: config.localIP,
        LocalPort: Number(config.localPort),
        RemoteIP: config.remoteIP,
        RemotePort: Number(config.remotePort),
        EQPType: config.eqpType,
        T3: Number(config.t3),
        T5: Number(config.t5),
        T6: Number(config.t6),
        T7: Number(config.t7),
        T8: Number(config.t8),
        ConnectionType: config.connectionType,
        Chamber: config.chamber,
        LoadPort: Number(config.loadPort),
        EventReportFile: config.eventReportFile,
        HeartbeatInterval: Number(config.heartbeatInterval),
        HeartbeatMaxFailures: Number(config.heartbeatMaxFailures),
        AutoReconnect: Boolean(config.autoReconnect)
      }
    },
    indexHandler(index) {
      return (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize + index + 1
    },
    isConnected(status) {
      return Number(status) === 1
    },
    isEnabled(enable) {
      return Number(enable) !== 0
    },
    statusMeta(status) {
      return this.isConnected(status)
        ? { text: 'Connected', tone: 'success' }
        : { text: 'DisConnected', tone: 'danger' }
    },
    connectionSummary(config) {
      if (config.connectionMode === 'PASSIVE') {
        return `监听 ${config.localIP}:${config.localPort}`
      }
      return `${config.remoteIP}:${config.remotePort}`
    },
    initSignalR() {
      if (this.connection) {
        return
      }

      const apiUrl = new URL(request.defaults.baseURL, window.location.origin)
      const logHubUrl = new URL('../logHub', apiUrl).href
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(logHubUrl, {
          skipNegotiation: true,
          transport: signalR.HttpTransportType.WebSockets,
          withCredentials: false
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build()

      this.connection.on('ReceiveLog', this.handleRealtimeLog)
      this.connection.onreconnected(() => {
        this.fetchData()
      })
      this.connection.start()
        .catch(err => console.error('SignalR Connection Error: ', err))
    },
    stopSignalR() {
      if (!this.connection) {
        return
      }

      this.connection.off('ReceiveLog', this.handleRealtimeLog)
      this.connection.stop().catch(() => {})
      this.connection = null
    },
    startStatusPolling() {
      if (this.statusRefreshTimer) {
        return
      }
      this.statusRefreshTimer = window.setInterval(() => {
        this.refreshStatuses()
      }, 5000)
    },
    stopStatusPolling() {
      if (this.statusRefreshTimer) {
        window.clearInterval(this.statusRefreshTimer)
        this.statusRefreshTimer = null
      }
    },
    async refreshStatuses() {
      if (this.statusRefreshInFlight || this.loading || !this.tableData.length || document.hidden) {
        return
      }

      this.statusRefreshInFlight = true
      try {
        const res = await GetList(this.pageInfo)
        const list = res && res.data && Array.isArray(res.data.list) ? res.data.list : []
        const latestById = new Map(list.map(item => [item.eqpId, item]))
        this.tableData = this.tableData.map(row => {
          const latest = latestById.get(row.eqpId)
          if (!latest) {
            return row
          }
          return {
            ...row,
            eqpStatus: latest.eqpStatus,
            eapStatus: latest.eapStatus,
            controlState: latest.controlState
          }
        })
      } catch (error) {
        // Keep the last known status; the next poll or SignalR event will refresh it.
      } finally {
        this.statusRefreshInFlight = false
      }
    },
    handleRealtimeLog(log) {
      if (!log || (log.type !== 'EqpState' && log.type !== 'EapState')) {
        return
      }

      const index = this.tableData.findIndex(item => item.eqpId === log.eqpId)
      if (index < 0) {
        return
      }

      const field = log.type === 'EqpState' ? 'eqpStatus' : 'eapStatus'
      this.tableData.splice(index, 1, {
        ...this.tableData[index],
        [field]: log.message
      })
    },
    async fetchData() {
      this.loading = true
      try {
        const res = await GetList(this.pageInfo)
        const list = res && res.data && Array.isArray(res.data.list) ? res.data.list : []
        const configEntries = await Promise.all(list.map(async item => {
          try {
            const configRes = await GetConnectionConfig(item.eqpId)
            if (configRes && configRes.return === 'OK' && configRes.data) {
              return [item.eqpId, this.mapConnectionConfig(configRes.data), '']
            }
            return [item.eqpId, null, (configRes && configRes.message) || '读取设备连接配置失败']
          } catch (error) {
            return [item.eqpId, null, error.message || '读取设备连接配置失败']
          }
        }))
        this.connectionConfigs = Object.fromEntries(configEntries.map(([eqpId, config]) => [eqpId, config]))
        const configErrors = Object.fromEntries(configEntries.map(([eqpId, , error]) => [eqpId, error]))
        this.tableData = list.map(item => ({
          ...item,
          connectionConfig: this.connectionConfigs[item.eqpId],
          connectionConfigError: configErrors[item.eqpId]
        }))
        this.total = res && res.data && Number.isFinite(Number(res.data.total)) ? Number(res.data.total) : list.length
      } finally {
        this.loading = false
      }
    },
    handleSearch() {
      this.pageInfo.pageNo = 1
      this.fetchData()
    },
    handleReset() {
      this.pageInfo.eqpId = ''
      this.pageInfo.eqpType = ''
      this.pageInfo.pageNo = 1
      this.fetchData()
    },
    handleSortChange({ prop, order }) {
      this.sortInfo = { prop, order }
    },
    handleAdd() {
      this.isEdit = false
      this.formData = this.getDefaultForm()
      this.showModal = true
    },
    handleEdit(row) {
      this.isEdit = true
      this.showModal = true
      this.formData = { ...row }
    },
    async handleConnectionConfig(row, configData = null) {
      this.configDeviceId = row.eqpId
      let config = this.connectionConfigs[row.eqpId]
      if (!config && configData) {
        config = this.mapConnectionConfig(configData)
        this.connectionConfigs = { ...this.connectionConfigs, [row.eqpId]: config }
      }
      if (!config) {
        try {
          const res = await GetConnectionConfig(row.eqpId)
          if (!res || res.return !== 'OK' || !res.data) {
            row.connectionConfigError = (res && res.message) || '未找到设备连接配置'
            this.$message.error(row.connectionConfigError)
            return
          }
          config = this.mapConnectionConfig(res.data)
          this.connectionConfigs = { ...this.connectionConfigs, [row.eqpId]: config }
          row.connectionConfigError = ''
          row.connectionConfig = config
        } catch (error) {
          row.connectionConfigError = error.message || '读取设备连接配置失败'
          this.$message.error(row.connectionConfigError)
          return
        }
      }
      this.configForm = { ...this.getDefaultConnectionConfig(), ...config }
      this.showConfigModal = true
    },
    async saveConnectionConfig() {
      this.configSaving = true
      try {
        const res = await SetConnectionConfig(this.configDeviceId, this.serializeConnectionConfig(this.configForm))
        if (res && res.return === 'OK') {
          const config = this.mapConnectionConfig(res.data)
          const savedFiles = Array.isArray(res.files) ? res.files : []
          this.connectionConfigs = { ...this.connectionConfigs, [this.configDeviceId]: config }
          const row = this.tableData.find(item => item.eqpId === this.configDeviceId)
          if (row) {
            row.connectionConfig = config
            row.connectionConfigError = ''
          }
          this.showConfigModal = false
          const filesText = savedFiles.length ? `已写入后端本机文件：${savedFiles.join('；')}。` : ''
          this.$message.success(`配置已保存，${filesText}启用时会加载最新配置；已启用设备请先禁用再启用`)
        } else {
          this.$message.error((res && res.message) || '保存连接配置失败')
        }
      } catch (error) {
        this.$message.error('保存连接配置失败')
      } finally {
        this.configSaving = false
      }
    },
    async handleEnableChange(row, enabled) {
      row.__switching = true
      const nextEnable = enabled ? 1 : 0
      const previousEnable = row.enable

      try {
        const res = await Edit({ ...row, enable: nextEnable })
        if (res.return === 'OK') {
          row.enable = nextEnable
          this.$message.success(enabled ? '设备已启用' : '设备已禁用')
        } else {
          row.enable = previousEnable
          this.$message.error(res.message || '操作失败')
        }
      } catch (error) {
        row.enable = previousEnable
        this.$message.error('操作失败')
      } finally {
        row.__switching = false
      }
    },
    async handleBatchEnable(enable) {
      if (this.selectedDevices.length === 0) {
        return
      }

      this.loading = true
      try {
        const results = []
        for (const row of [...this.selectedDevices]) {
          try {
            const res = await Edit({ ...row, enable })
            if (res.return === 'OK') {
              results.push({ row, success: true })
            } else {
              results.push({ row, success: false, message: res.message || '操作失败' })
            }
          } catch (error) {
            results.push({ row, success: false, message: error.message || '请求失败' })
          }
        }

        const failed = results.filter(result => !result.success)
        await this.fetchData()
        if (failed.length === 0) {
          this.$message.success(enable === 1 ? '批量启用完成' : '批量禁用完成')
        } else {
          const failedNames = failed.map(result => result.row.eqpId).join('、')
          this.$message.error(`${failedNames} 启停失败：${failed[0].message}`)
        }
      } finally {
        this.loading = false
      }
    },
    handleDelete(row) {
      this.$confirm(`确定删除设备 ${row.eqpId || ''} 吗？`, '删除确认', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Delete(row).then(res => {
          if (res.return === 'OK') {
            this.$message.success('删除成功')
            this.fetchData()
          } else {
            this.$message.error(res.message)
          }
        })
      }).catch(() => {})
    },
    handleBatchDelete() {
      if (this.selectedDevices.length === 0) {
        this.$message.warning('请至少选择一台设备')
        return
      }

      const ids = this.selectedDevices.map(item => item.eqpId)
      this.$confirm(`确定删除选中的 ${ids.length} 台设备吗？`, '删除确认', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Deletes(ids).then(res => {
          if (res.return === 'OK') {
            this.$message.success('删除成功')
            this.fetchData()
          } else {
            this.$message.error(res.message)
          }
        })
      }).catch(() => {})
    },
    handleSelectionChange(selection) {
      this.selectedDevices = selection
    },
    handleSizeChange() {
      this.pageInfo.pageNo = 1
    },
    handlePageChange() {},
    async handleDialogSubmit(formData) {
      this.loading = true
      const isEdit = this.isEdit
      try {
        const res = await (isEdit ? Edit(formData) : Add(formData))
        if (res.return === 'OK') {
          this.$message.success(isEdit ? '修改成功' : '添加成功，已生成默认连接参数')
          this.showModal = false
          await this.fetchData()
          if (!isEdit) {
            const row = this.tableData.find(item => item.eqpId === formData.eqpId)
            const configData = res.data || null
            await this.handleConnectionConfig(row || { eqpId: formData.eqpId }, configData)
            this.$message.info('请确认并修改设备 IP 和端口，保存后重启 EAPServer 加载新设备')
          }
        } else {
          this.$message.error(res.message)
        }
      } finally {
        this.loading = false
      }
    },
    handleMoreCommand(command, row) {
      if (command === 'refresh') {
        this.fetchData()
      }
      if (command === 'copy') {
        this.copyText(row.eqpId || '')
      }
    },
    copyText(text) {
      if (!text) {
        return
      }

      if (navigator.clipboard) {
        navigator.clipboard.writeText(text)
      }
      this.$message.success('已复制')
    }
  }
}
</script>

<style lang="scss" scoped>
.page-shell {
  min-height: calc(100vh - 64px);
  padding: 16px;
  background: #f5f7fa;
  color: #182433;
  text-align: left;
  box-sizing: border-box;
}

.filter-panel,
.table-panel,
.metric-card {
  border: 1px solid #dce3ec;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 1px 2px rgba(24, 36, 51, 0.05);
}

.filter-panel {
  padding: 14px;
}

.filter-grid {
  display: grid;
  grid-template-columns: minmax(180px, 240px) minmax(180px, 240px) minmax(360px, 1fr);
  gap: 12px;
  align-items: center;
}

.toolbar,
.icon-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.toolbar--right {
  justify-content: flex-end;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(140px, 1fr));
  gap: 12px;
  margin: 12px 0;
}

.metric-card {
  min-height: 78px;
  padding: 12px;
  border-left: 4px solid #8a96a8;
  box-sizing: border-box;
}

.metric-card span {
  color: #596679;
  font-size: 12px;
  font-weight: 700;
}

.metric-card strong {
  display: block;
  margin-top: 10px;
  color: #162235;
  font-size: 26px;
  line-height: 1;
}

.metric-card--success {
  border-left-color: #52c41a;
}

.metric-card--info {
  border-left-color: #1890ff;
}

.metric-card--danger {
  border-left-color: #ff4d4f;
}

.metric-card--neutral {
  border-left-color: #8a96a8;
}

.table-panel {
  padding: 12px;
}

.table-toolbar,
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.table-toolbar {
  margin-bottom: 10px;
}

.selection-text {
  color: #596679;
  font-size: 13px;
}

.selection-text b {
  color: #1890ff;
}

.connection-summary {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
  white-space: nowrap;
}

.connection-summary span {
  overflow: hidden;
  text-overflow: ellipsis;
}

.connection-config-form :deep(.el-input-number) {
  width: 100%;
}

.pagination-bar {
  justify-content: flex-end;
  position: sticky;
  bottom: 0;
  z-index: 5;
  margin-top: 12px;
  padding-top: 10px;
  background: #fff;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-height: 24px;
  padding: 3px 9px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 700;
  line-height: 1;
  white-space: nowrap;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
}

.status-badge--success {
  color: #13744d;
  background: #e9f7f0;
}

.status-badge--danger {
  color: #b42318;
  background: #fff0ed;
}

:deep(.el-table th.el-table__cell) {
  background: #f5f7fa;
  color: #182433;
  font-weight: 800;
}

:deep(.el-table__row:hover > td.el-table__cell) {
  background: #f2f8ff;
}

:deep(.el-button) {
  border-radius: 6px;
}

:deep(.el-input__wrapper) {
  border-radius: 6px;
}

@media (max-width: 960px) {
  .filter-grid,
  .summary-grid {
    grid-template-columns: 1fr;
  }

  .table-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .toolbar--right {
    justify-content: flex-start;
  }
}
</style>
