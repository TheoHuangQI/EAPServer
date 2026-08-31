<template>
  <div class="app-container">
    <!-- 操作栏 -->
    <el-card v-if="false" class="filter-card" shadow="never">
      <el-row :gutter="20">
        <el-col :xs="4" :sm="4" :md="4" :lg="4">
          <el-input
            v-model="pageInfo.eqpId"
            placeholder="设备ID"
            clearable
            @keyup.enter="handleSearch"
          />
        </el-col>

        <el-col :xs="4" :sm="4" :md="4" :lg="4">
          <el-input
            v-model="pageInfo.vid"
            placeholder="VID"
            clearable
            @keyup.enter="handleSearch"
          />
        </el-col>

        <el-col :xs="8" :sm="8" :md="8" :lg="8" class="operation-col">
          <el-button-group>
            <el-button type="primary" @click="handleSearch">
              <el-icon><Search /></el-icon>
              查询
            </el-button>

            <el-button type="success" @click="handleAdd">
              <el-icon><Plus /></el-icon>
              新增
            </el-button>

            <el-button
              type="danger"
              :disabled="selectedRows.length === 0"
              @click="handleBatchDelete"
            >
              <el-icon><Delete /></el-icon>
              批量删除
            </el-button>
          </el-button-group>
        </el-col>
      </el-row>
    </el-card>

    <!-- 数据表格 -->
    <el-card v-if="false" class="table-card" shadow="never">
      <el-table
        :data="paginatedData"
        style="width: 100%"
        stripe
        v-loading="loading"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="55" align="center" />

        <el-table-column
          :index="indexHandler"
          label="序号"
          type="index"
          width="80"
          align="center"
        />

        <el-table-column prop="vid" label="VID" align="center" />

        <el-table-column prop="vidType" label="VID TYPE" align="center">
          <template #default="scope">
            <span v-if="scope.row.vidType === 0">SVID</span>
            <span v-else-if="scope.row.vidType === 1">DVID</span>
            <span v-else-if="scope.row.vidType === 2">ECID</span>
          </template>
        </el-table-column>

        <el-table-column prop="eqpId" label="设备号" align="center" />
        <el-table-column prop="interval" label="采集间隔" align="center" />
        <el-table-column prop="count" label="采集次数" align="center" />

        <el-table-column prop="triggerType" label="触发方式" align="center">
          <template #default="scope">
            <span v-if="scope.row.triggerType === 0">EQP</span>
            <span v-else-if="scope.row.triggerType === 1">EVENT</span>
          </template>
        </el-table-column>

        <el-table-column prop="startEvent" label="开始事件" align="center" />
        <el-table-column prop="endEvent" label="结束事件" align="center" />

        <el-table-column prop="compareType" label="比对方式" align="center">
          <template #default="scope">
            <span v-if="scope.row.compareType === 0">值比对</span>
            <span v-else-if="scope.row.compareType === 1">范围比对</span>
          </template>
        </el-table-column>

        <el-table-column prop="compareValue" label="比对值" align="center" />
        <el-table-column prop="compareMax" label="最大值" align="center" />
        <el-table-column prop="compareMin" label="最小值" align="center" />
        <el-table-column prop="allowNumber" label="允许次数" align="center" />
        <el-table-column prop="action" label="执行动作" align="center" />

        <!-- 操作列 -->
        <el-table-column label="操作" width="150" align="center">
          <template #default="scope">
            <el-tooltip content="编辑" placement="top">
              <el-button
                type="primary"
                circle
                size="small"
                @click="handleEdit(scope.row)"
              >
                <el-icon><Edit /></el-icon>
              </el-button>
            </el-tooltip>

            <el-tooltip content="删除" placement="top">
              <el-button
                type="danger"
                circle
                size="small"
                @click="handleDelete(scope.row)"
              >
                <el-icon><Delete /></el-icon>
              </el-button>
            </el-tooltip>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
      <el-pagination
        class="pagination-wrapper"
        :current-page="pageInfo.pageNo"
        :page-sizes="[5, 10, 20]"
        :page-size="pageInfo.pageSize"
        :total="total"
        layout="total, sizes, prev, pager, next, jumper"
        @size-change="handleSizeChange"
        @current-change="handlePageChange"
      />
    </el-card>

    <el-row :gutter="20">
      <!-- 查询条件 -->
      <el-col style="margin-bottom: 10px" :span="24">
        <el-select
          v-model="pageInfo.abnormalFlag"
          clearable
          placeholder="异常类型"
          style="width: 240px; margin-right: 10px"
        >
          <el-option
            v-for="item in abnormalOptions"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </el-select>

        <el-date-picker
          v-model="pageInfo.timeRange"
          type="datetimerange"
          range-separator="至"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
          value-format="YYYY-MM-DD HH:mm:ss"
          clearable
        />

        <el-button
          type="primary"
          @click="handleSearch"
          style="margin-left: 10px"
        >
          <el-icon><Search /></el-icon>
          查询
        </el-button>

        <el-button-group class="chart-actions">
          <el-button :disabled="!chartSeries.length" @click="showAllSeries">
            显示全部
          </el-button>

          <el-button :disabled="!chartSeries.length" @click="hideAllSeries">
            隐藏全部
          </el-button>

          <el-button
            type="success"
            :disabled="!chartSeries.length"
            @click="enableBrushZoom"
          >
            框选放大
          </el-button>

          <el-button :disabled="zoomHistory.length === 0" @click="restorePreviousZoom">
            恢复上一步
          </el-button>

          <el-button :disabled="!originalTimeRange" @click="restoreOriginalZoom">
            恢复原样
          </el-button>
        </el-button-group>
      </el-col>

      <!-- 图表 -->
      <el-col :span="24">
        <el-card class="chart-card">
          <div ref="chart" style="height: 750px"></div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script>
import {
  GetList,
  Add,
  Edit as EditApi,
  Delete as DeleteApi
} from '@/api/dataRule'

import {
  GetSvidTrend,
  GetSvidTrendwithAbnormal
} from '@/api/vidValue'

import * as echarts from 'echarts'

export default {
  name: 'DataHistoryIndex',


  data() {
    return {
      chart: null,
      chartSeries: [],
      chartSeriesData: [],
      legendSelected: {},
      originalTimeRange: null,
      zoomHistory: [],
      isApplyingZoom: false,
      isBrushZoomActive: false,
      brushStartPixel: null,
      brushRectEl: null,
      currentMaxPointsPerSeries: 400,
      lastRightClickTime: 0,

      abnormalOptions: [
        { value: 0, label: '正常' },
        { value: 1, label: '与上一次值相同' },
        { value: 2, label: '值为 0' },
        { value: 4, label: '首次采集' }
      ],

      loading: false,

      selectedRows: [],

      showModal: false,

      isEdit: false,

      tableData: [],

      total: 0,

      formData: this.getDefaultForm(),

      pageInfo: {
        eqpId: '',
        vid: '',
        timeRange: [],
        abnormalFlag: '',
        pageNo: 1,
        pageSize: 10
      }
    }
  },

  computed: {
    paginatedData() {
      const start =
        (this.pageInfo.pageNo - 1) *
        this.pageInfo.pageSize

      const end =
        this.pageInfo.pageNo *
        this.pageInfo.pageSize

      return this.tableData.slice(start, end)
    }
  },

  created() {
    this.fetchData()
  },

  unmounted() {
    this.destroyChart()
  },

  deactivated() {
    this.destroyChart()
  },

  activated() {
    this.rebuildChart()
  },

  methods: {
    showAllSeries() {
      this.disableBrushZoom()
      this.setAllSeriesVisible(true)
    },

    hideAllSeries() {
      this.disableBrushZoom()
      this.setAllSeriesVisible(false)
    },
    destroyChart() {
        if (this.chart) {
          this.clearBrushRect()

          // 移除所有事件防止内存泄漏
          this.chart.off('legendselectchanged')
          this.chart.off('click')
          this.chart.getZr().off('dblclick')
          this.chart.getZr().off('contextmenu')
          this.chart.getZr().off('mousedown')
          this.chart.getZr().off('mousemove')
          this.chart.getZr().off('mouseup')

          window.removeEventListener('resize', this.chartResizeHandler)

          this.chart.dispose()
          this.chart = null
        }

        this.brushRectEl = null
        this.isBrushZoomActive = false
        this.brushStartPixel = null
      },

      // ⚡ 图表崩溃时自动重建
      rebuildChart() {
        const container = this.$refs?.chart
        if (!container) return

        try {
          this.chart?.dispose()
        } catch (e) {
          // ignore
        }
        this.chart = null

        this.$nextTick(() => {
          try {
            this.chart = echarts.init(container)
            if (this.chartSeriesData.length > 0) {
              this.drawChart(this.chartSeriesData)
            }
          } catch (e) {
            console.error('Chart rebuild failed:', e)
          }
        })
      },

      getDefaultForm() {
        return {
          vidType: '',
          vid: '',
          interval: null,
          count: null,
          eqpId: '',
          triggerType: 0,
          startEvent: '',
          endEvent: '',
          compareType: 0,
          compareValue: '',
          compareMin: '',
          compareMax: '',
          limitTimes: '',
          actions: [],
          action: ''
        }
      },

    indexHandler(index) {
      return (
        (this.pageInfo.pageNo - 1) *
          this.pageInfo.pageSize +
        index +
        1
      )
    },

    async fetchData() {
      this.loading = true

      try {
        const res = await GetList(this.pageInfo)

        this.tableData = res.data.list || []

        this.total = res.data.total || 0
      } catch (error) {
        console.error(error)

        this.$message.error('数据加载失败')
      } finally {
        this.loading = false
      }
    },

    async handleSearch() {
      if (
        !this.pageInfo.timeRange ||
        this.pageInfo.timeRange.length !== 2
      ) {
        this.$message.warning('请选择时间范围')
        return
      }

      this.pageInfo.pageNo = 1
      this.originalTimeRange = [...this.pageInfo.timeRange]
      this.zoomHistory = []
      this.currentMaxPointsPerSeries = 400
      this.disableBrushZoom()

      await Promise.all([this.fetchData(), this.fetchSvidTrend()])
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

    handleDelete(row) {
      this.$confirm(
        '确定删除设备吗？',
        '警告',
        {
          type: 'warning',
          confirmButtonText: '确认删除',
          cancelButtonText: '取消',
          confirmButtonClass:
            'el-button--danger'
        }
      )
        .then(() => {
          DeleteApi([row.id]).then(res => {
            if (res.return === 'OK') {
              this.$message.success('删除成功')

              this.fetchData()
            } else {
              this.$message.error(
                res.message
              )
            }
          })
        })
        .catch(() => {})
    },

    handleBatchDelete() {
      if (this.selectedRows.length === 0) {
        this.$message.warning(
          '请至少选择一条记录'
        )

        return
      }

      this.$confirm(
        `确定删除选中的 ${this.selectedRows.length} 条记录吗？`,
        '警告',
        {
          type: 'warning',
          confirmButtonText: '确认删除',
          cancelButtonText: '取消',
          confirmButtonClass:
            'el-button--danger'
        }
      )
        .then(() => {
          DeleteApi(this.selectedRows).then(
            res => {
              if (res.return === 'OK') {
                this.$message.success(
                  '删除成功'
                )

                this.fetchData()
              } else {
                this.$message.error(
                  res.message
                )
              }
            }
          )
        })
        .catch(() => {})
    },

    handleSelectionChange(selection) {
      this.selectedRows = selection.map(
        item => item.id
      )
    },

    handleSizeChange(val) {
      this.pageInfo.pageSize = val

      this.pageInfo.pageNo = 1
    },

    handlePageChange(val) {
      this.pageInfo.pageNo = val
    },

    getActionStr(action) {
      return action === 0
        ? 'StopEqp'
        : 'HoldLot'
    },

    async handleDialogSubmit(formData) {
      this.loading = true

      const submitData = {
        ...formData,
        action: (formData.actions || [])
          .map(element => this.getActionStr(element))
          .join(';') + (formData.actions && formData.actions.length ? ';' : ''),
        actions: []
      }

      try {
        const res = this.isEdit
          ? await EditApi(submitData)
          : await Add(submitData)

        if (res.return === 'OK') {
          this.$message.success(
            this.isEdit ? '修改成功' : '添加成功'
          )
          this.fetchData()
        } else {
          this.$message.error(res.message)
        }
      } catch (error) {
        console.error(error)
        this.$message.error('提交失败')
      } finally {
        this.loading = false
      }
    },

        drawChart(seriesData) {
      // Always destroy and recreate to prevent internal state corruption
      try {
        if (this.chart) {
          this.chart.clear()
          this.chart.dispose()
        }
      } catch (e) {}

      this.chart = echarts.init(this.$refs.chart)

      this.chartResizeHandler = () => {
        try { this.chart?.resize() } catch (e) {}
      }

      window.addEventListener('resize', this.chartResizeHandler)

      const sourceData = Array.isArray(seriesData)
        ? seriesData
        : []

      const normalizedSeriesData = sourceData
        .filter(item => item && Array.isArray(item.points))
        .map(item => ({
          ...item,
          points: item.points
            .filter(point => point && point.time !== undefined && point.time !== null)
            .map(point => {
              const value = Number(point.value)

              return {
                ...point,
                value: Number.isFinite(value) ? value : null
              }
            })
        }))
        .filter(item => item.points.length > 0)

      if (normalizedSeriesData.length === 0) {
        this.clearBrushRect()
        this.chart.clear()
        this.chartSeries = []
        this.chartSeriesData = []
        this.legendSelected = {}

        return
      }

      const nameCounts = normalizedSeriesData.reduce((counts, item) => {
        const baseName = item.name || `VID-${item.vid}`
        counts[baseName] = (counts[baseName] || 0) + 1
        return counts
      }, {})

      // 构建 series，并确保 legend 名称唯一（避免重复名称导致图例行为异常）
      const series = normalizedSeriesData.map((item, idx) => {
        const baseName = item.name || `VID-${item.vid}`
        // 如果存在重复的 baseName，则在后面附加索引以保证唯一性
        const name = nameCounts[baseName] > 1 ? `${baseName} (${idx + 1})` : baseName

        return {
          id: `vid-${item.vid}-${idx}`,
          name,
          type: 'line',
          coordinateSystem: 'cartesian2d',
          xAxisIndex: 0,
          yAxisIndex: 0,
          symbol: 'circle',
          symbolSize: 6,
          data: item.points.map(point => ({
            value: [point.time, point.value],
            point
          }))
        }
      })

      // 过滤掉无效的 series，防止 ECharts 渲染错误
      const validSeries = series.filter(s => s && typeof s === 'object' && s.type)

      // 明确 legend 数据，确保点击可以正确关联到 series
      const legendData = validSeries.map(s => s.name)

      this.chartSeries = validSeries
      this.chartSeriesData = normalizedSeriesData
      this.legendSelected = legendData.reduce((result, name) => {
        result[name] = this.legendSelected[name] !== false
        return result
      }, {})

      const option = {
        animation: false,
        tooltip: {
          trigger: 'axis',
          axisPointer: { type: 'cross' },
          formatter(params) {
            const rows = Array.isArray(params) ? params : [params]
            const firstPoint = rows[0] && rows[0].data && rows[0].data.point
            const time = firstPoint ? firstPoint.time : ''

            return [
              time,
              ...rows.map(row => {
                const point = row.data && row.data.point
                const value = point ? point.value : ''
                return `${row.marker}${row.seriesName}：${value}`
              })
            ].join('<br/>')
          }
        },

        // legend 优化：启用 tooltip、分页按钮并收窄 item，大量 series 时可滚动并保持可点击
        legend: {
          data: legendData,
          selected: this.legendSelected,
          type: 'scroll',
          orient: 'vertical',
          right: '4%',
          top: '6%',
          bottom: '6%',
          itemWidth: 12,
          itemHeight: 12,
          textStyle: { width: 160 },
          tooltip: { show: true },
          pageButtonItemGap: 6,
          pageButtonPosition: 'end',
          selectedMode: 'multiple'
        },

        // grid 使用百分比右侧留白，配合 legend 的百分比位置避免重叠
        grid: [
          {
            left: 60,
            right: '30%',
            top: 40,
            bottom: 60,
            containLabel: true
          }
        ],

        xAxis: [
          {
            type: 'time',
            gridIndex: 0,
            name: '时间',
            axisLabel: {
              rotate: 45
            }
          }
        ],

        yAxis: [
          {
            type: 'value',
            gridIndex: 0,
            name: '值'
          }
        ],

        series: validSeries
      }

      // ⚡ 使用明确的对象参数，避免 ECharts 内部状态混乱
      try {
        this.chart.setOption(option, {
          notMerge: true,
          lazyUpdate: false
        })
      } catch (error) {
        console.error('Chart setOption failed, rebuilding:', error)
        this.rebuildChart()
        return
      }

      this.bindChartEvents(this.chart, normalizedSeriesData)
    },

    async fetchSvidTrend(options = {}) {
      const timeRange = options.timeRange || this.pageInfo.timeRange
      const maxPointsPerSeries = options.maxPointsPerSeries || this.currentMaxPointsPerSeries

      const params = {
        eqpId: this.pageInfo.eqpId,
        startTime: timeRange[0],
        endTime: timeRange[1],
        maxPointsPerSeries
      }

      try {
        const res = await (
          this.pageInfo.abnormalFlag !== null &&
          this.pageInfo.abnormalFlag !== undefined &&
          this.pageInfo.abnormalFlag !== ''
            ? GetSvidTrendwithAbnormal({
                ...params,
                AbnormalFlag: this.pageInfo.abnormalFlag
              })
            : GetSvidTrend(params)
        )

        if (res.return === 'OK') {
          this.drawChart(res.data)
        } else {
          this.$message.error(res.MESSAGE || res.message || '趋势数据加载失败')
        }
      } catch (error) {
        console.error(error)
        this.$message.error('趋势数据加载失败')
      }
    },

    bindChartEvents(chart, seriesData) {
      chart.off('click')
      chart.getZr().off('dblclick')
      chart.getZr().off('contextmenu')
      chart.getZr().off('mousedown')
      chart.getZr().off('mousemove')
      chart.getZr().off('mouseup')

      // 统一管理图例选择事件，确保组件状态与图表状态同步
      chart.on('legendselectchanged', params => {
        this.disableBrushZoom()
        // 只更新本地状态，不二次 setOption 避免干扰 ECharts 内部渲染
        this.legendSelected = { ...params.selected }
      })

      chart.on('click', params => {
        if (params.componentType !== 'series') {
          return
        }

        const seriesItem = seriesData.find(item => {
          const baseName = item.name || `VID-${item.vid}`
          return (
            params.seriesName === baseName ||
            params.seriesName.startsWith(`${baseName} (`)
          )
        })

        if (!seriesItem || !Array.isArray(seriesItem.points)) {
          return
        }

        const point = params.data && params.data.point ? params.data.point : null

        if (!point) {
          return
        }

        this.$alert(
          `
VID：${seriesItem.vid || seriesItem.name}
名称：${seriesItem.name || ''}
值：${point.value}
时间：${point.time}
异常：${this.getAbnormalText(point.abnormal_flag)}
          `,
          '数据点详情',
          { confirmButtonText: '关闭' }
        )
      })

      chart.getZr().on('dblclick', event => {
        this.restoreOriginalZoom()
      })

      chart.getZr().on('mousedown', event => {
        const rawEvent = event.event

        if (
          this.isBrushZoomActive &&
          rawEvent &&
          rawEvent.button === 0 &&
          this.chart.containPixel({ gridIndex: 0 }, [event.offsetX, event.offsetY])
        ) {
          this.brushStartPixel = [event.offsetX, event.offsetY]
          rawEvent.preventDefault()
          return
        }

        if (!event.event || event.event.button !== 2) {
          return
        }

        const now = Date.now()

        if (now - this.lastRightClickTime < 400) {
          this.restorePreviousZoom()
          this.lastRightClickTime = 0
          return
        }

        this.lastRightClickTime = now
      })

      chart.getZr().on('mousemove', event => {
        if (!this.isBrushZoomActive || !this.brushStartPixel) {
          return
        }

        this.renderBrushRect(this.brushStartPixel, [event.offsetX, event.offsetY])
      })

      chart.getZr().on('mouseup', event => {
        if (!this.isBrushZoomActive || !this.brushStartPixel) {
          return
        }

        const startPixel = this.brushStartPixel
        const endPixel = [event.offsetX, event.offsetY]
        this.brushStartPixel = null

        this.handleBrushZoomByPixels(startPixel, endPixel)
      })

      chart.getZr().on('contextmenu', event => {
        event.event.preventDefault()
      })
    },

    getAbnormalText(flag) {
      const numericFlag = Number(flag)

      if (!Number.isFinite(numericFlag) || numericFlag === 0) {
        return '正常'
      }

      const labels = []

      if ((numericFlag & 1) === 1) {
        labels.push('与上一次值相同')
      }

      if ((numericFlag & 2) === 2) {
        labels.push('值为 0')
      }

      if ((numericFlag & 4) === 4) {
        labels.push('首次采集')
      }

      return labels.length ? labels.join('、') : `未知(${numericFlag})`
    },

    setAllSeriesVisible(visible) {
      if (!this.chart || this.chartSeries.length === 0) {
        return
      }

      this.disableBrushZoom()

      this.legendSelected = this.chartSeries.reduce((result, item) => {
        result[item.name] = visible
        return result
      }, {})

      // ⚡ 直接通过 ECharts API 设置，避免二次 setOption
      try {
        this.chart.dispatchAction({
          type: 'legendSelect',
          name: this.chartSeries[0].name
        })
      } catch (e) {
        // ignore
      }
    },

    enableBrushZoom() {
      if (!this.chart) {
        return
      }

      if (this.isBrushZoomActive) {
        this.disableBrushZoom()
        return
      }

      this.isBrushZoomActive = true
      this.chart.getZr().setCursorStyle('crosshair')

      this.$message.info('按住鼠标左键框选需要细看的时间段')
    },

    disableBrushZoom(clearAreas = true) {
      if (!this.chart) {
        this.isBrushZoomActive = false
        this.brushStartPixel = null
        this.brushRectEl = null
        return
      }

      this.isBrushZoomActive = false
      this.brushStartPixel = null
      this.chart.getZr().setCursorStyle('default')

      if (clearAreas) {
        this.clearBrushRect()
      }
    },

    renderBrushRect(startPixel, endPixel) {
      const x = Math.min(startPixel[0], endPixel[0])
      const y = Math.min(startPixel[1], endPixel[1])
      const width = Math.abs(endPixel[0] - startPixel[0])
      const height = Math.abs(endPixel[1] - startPixel[1])

      if (!this.brushRectEl) {
        this.brushRectEl = new echarts.graphic.Rect({
          silent: true,
          z: 100,
          shape: { x, y, width, height },
          style: {
            fill: 'rgba(64, 158, 255, 0.12)',
            stroke: 'rgba(64, 158, 255, 0.55)',
            lineWidth: 1
          }
        })

        this.chart.getZr().add(this.brushRectEl)
        return
      }

      this.brushRectEl.setShape({
        x,
        y,
        width,
        height
      })
    },

    clearBrushRect() {
      if (!this.chart) {
        return
      }

      if (this.brushRectEl) {
        this.chart.getZr().remove(this.brushRectEl)
        this.brushRectEl = null
      }
    },

    async handleBrushZoomByPixels(startPixel, endPixel) {
      const width = Math.abs(endPixel[0] - startPixel[0])

      if (width < 8 || !this.pageInfo.timeRange || this.pageInfo.timeRange.length !== 2) {
        this.disableBrushZoom()
        return
      }

      const startCoord = this.chart.convertFromPixel({ gridIndex: 0 }, startPixel)
      const endCoord = this.chart.convertFromPixel({ gridIndex: 0 }, endPixel)

      if (!Array.isArray(startCoord) || !Array.isArray(endCoord)) {
        this.disableBrushZoom()
        return
      }

      const start = this.formatChartTime(Math.min(startCoord[0], endCoord[0]))
      const end = this.formatChartTime(Math.max(startCoord[0], endCoord[0]))

      if (!start || !end || start === end) {
        this.disableBrushZoom()
        return
      }

      this.disableBrushZoom()
      this.zoomHistory.push([...this.pageInfo.timeRange])
      this.pageInfo.timeRange = [start, end]
      this.currentMaxPointsPerSeries = 2000
      this.isApplyingZoom = true

      try {
        await this.fetchSvidTrend({
          timeRange: this.pageInfo.timeRange,
          maxPointsPerSeries: this.currentMaxPointsPerSeries
        })
      } finally {
        this.isApplyingZoom = false
      }
    },

    async restorePreviousZoom() {
      if (this.zoomHistory.length === 0) {
        return
      }

      this.disableBrushZoom()
      this.pageInfo.timeRange = this.zoomHistory.pop()
      this.currentMaxPointsPerSeries = this.zoomHistory.length === 0 ? 400 : 2000
      await this.fetchSvidTrend({
        timeRange: this.pageInfo.timeRange,
        maxPointsPerSeries: this.currentMaxPointsPerSeries
      })
    },

    async restoreOriginalZoom() {
      if (!this.originalTimeRange) {
        return
      }

      this.disableBrushZoom()
      this.pageInfo.timeRange = [...this.originalTimeRange]
      this.zoomHistory = []
      this.currentMaxPointsPerSeries = 400
      await this.fetchSvidTrend({
        timeRange: this.pageInfo.timeRange,
        maxPointsPerSeries: this.currentMaxPointsPerSeries
      })
    },

    formatChartTime(value) {
      const date = value instanceof Date ? value : new Date(value)

      if (Number.isNaN(date.getTime())) {
        return ''
      }

      const pad = number => String(number).padStart(2, '0')

      return [
        date.getFullYear(),
        pad(date.getMonth() + 1),
        pad(date.getDate())
      ].join('-') + ' ' + [
        pad(date.getHours()),
        pad(date.getMinutes()),
        pad(date.getSeconds())
      ].join(':')
    }
  }
}
</script>

<style lang="scss" scoped>
.app-container {
  padding: 20px;
  background-color: #f5f7fa;

  .filter-card {
    margin-bottom: 20px;
    border-radius: 8px;
    border: 1px solid #ebeef5;

    .operation-col {
      text-align: left;

      @media (max-width: 768px) {
        margin-top: 10px;
      }
    }
  }

  .table-card {
    border-radius: 8px;
    border: 1px solid #ebeef5;

    :deep(.el-table th) {
      background-color: #f5f7fa;
    }

    .pagination-wrapper {
      margin-top: 20px;
      text-align: right;
    }
  }

  .el-button-group {
    .el-button {
      & + .el-button {
        margin-left: 8px;
      }
    }
  }

  .chart-actions {
    margin-left: 10px;
    vertical-align: middle;
  }
}

.el-dialog {
  border-radius: 8px;

  .el-form-item {
    margin-bottom: 22px;
  }
}
</style>
