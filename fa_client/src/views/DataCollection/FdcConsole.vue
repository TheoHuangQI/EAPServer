<template>
  <div class="fdc-console">
    <section class="page-header">
      <div>
        <h1>FDC 数据采集</h1>
        <p>Data Collection & Monitoring Console</p>
      </div>
      <div class="header-actions">
        <span class="status-badge" :class="'status-badge--' + systemStatus.tone">
          <span class="status-dot"></span>
          {{ systemStatus.text }}
        </span>
        <span class="last-update">Last Update: {{ lastRefreshText }}</span>
        <el-button type="primary" :loading="loading" @click="reloadAll">
          <el-icon><Refresh /></el-icon>
          刷新
        </el-button>
      </div>
    </section>

    <section v-if="errorMessage" class="error-panel">
      <div>
        <strong>Unable to load FDC data.</strong>
        <span>{{ errorMessage }}</span>
      </div>
      <el-button type="primary" plain @click="reloadAll">Retry</el-button>
    </section>

    <el-tabs v-model="activeTab" class="fdc-tabs" @tab-change="handleTabChange">
      <el-tab-pane label="Overview 总览" name="overview" />
      <el-tab-pane label="Collection 当前采集" name="collection" />
      <el-tab-pane label="Collection Plans 采集计划" name="plans" />
      <el-tab-pane label="Data Quality 数据质量" name="quality" />
      <el-tab-pane label="History 历史记录" name="history" />
    </el-tabs>

    <section v-show="activeTab === 'overview'" class="tab-panel">
      <div class="kpi-grid">
        <article class="kpi-card kpi-card--info">
          <span>Data Sources</span>
          <strong>{{ equipmentList.length }}</strong>
          <small>{{ connectedEquipmentCount }} Connected</small>
        </article>
        <article class="kpi-card kpi-card--success">
          <span>Active Collections</span>
          <strong>{{ activeCollectionCount }}</strong>
          <small>Based on rules + latest samples</small>
        </article>
        <article class="kpi-card kpi-card--warning">
          <span>Samples / sec</span>
          <strong>N/A</strong>
          <small>后端未提供实时速率</small>
        </article>
        <article class="kpi-card" :class="'kpi-card--' + qualityOverview.tone">
          <span>Data Quality</span>
          <strong>{{ qualityOverview.label }}</strong>
          <small>{{ qualityOverview.good }} Good / {{ qualityOverview.total }} Samples</small>
        </article>
      </div>

      <div class="content-grid content-grid--overview">
        <section class="panel">
          <div class="panel-head">
            <h2>Collection Status</h2>
            <span class="panel-meta">{{ collectionRows.length }} Sources</span>
          </div>
          <el-table
            v-loading="loading"
            :data="collectionRows"
            height="320"
            stripe
            highlight-current-row
            row-key="eqpId"
            @row-click="selectEquipmentFromRow"
          >
            <el-table-column prop="eqpId" label="Equipment" min-width="120" sortable show-overflow-tooltip />
            <el-table-column label="Status" width="130" sortable>
              <template #default="scope">
                <span class="status-badge" :class="'status-badge--' + scope.row.status.tone">
                  <span class="status-dot"></span>
                  {{ scope.row.status.text }}
                </span>
              </template>
            </el-table-column>
            <el-table-column prop="plan" label="Plan" min-width="120" show-overflow-tooltip />
            <el-table-column prop="job" label="Job / Lot" min-width="130" show-overflow-tooltip />
            <el-table-column prop="sampling" label="Sampling" width="110" align="right" />
            <el-table-column prop="samples" label="Samples" width="110" align="right" />
          </el-table>
        </section>

        <section class="panel">
          <div class="panel-head">
            <h2>Data Pipeline</h2>
            <span class="panel-meta">Current Capability</span>
          </div>
          <div class="pipeline-list">
            <div v-for="item in pipelineRows" :key="item.name" class="pipeline-item">
              <span>{{ item.name }}</span>
              <span class="status-badge" :class="'status-badge--' + item.tone">
                <span class="status-dot"></span>
                {{ item.text }}
              </span>
            </div>
          </div>
          <el-empty v-if="pipelineRows.length === 0" :image-size="72" description="No pipeline status available" />
        </section>
      </div>

      <div class="content-grid content-grid--overview">
        <section class="panel">
          <div class="panel-head">
            <h2>Data Sources</h2>
            <span class="panel-meta">Equipment Detail</span>
          </div>
          <div class="source-list">
            <button
              v-for="source in dataSources"
              :key="source.eqpId"
              type="button"
              class="source-card"
              :class="{ 'source-card--active': source.eqpId === selectedEquipmentId }"
              @click="selectedEquipmentId = source.eqpId"
            >
              <span>
                <b>{{ source.eqpId }}</b>
                <small>{{ source.eqpType || 'N/A' }}</small>
              </span>
              <span class="status-badge" :class="'status-badge--' + source.connection.tone">
                <span class="status-dot"></span>
                {{ source.connection.text }}
              </span>
              <dl>
                <div>
                  <dt>Variables</dt>
                  <dd>{{ source.variableCount }}</dd>
                </div>
                <div>
                  <dt>Sampling</dt>
                  <dd>{{ source.samplingRate }}</dd>
                </div>
                <div>
                  <dt>Last Collection</dt>
                  <dd>{{ source.lastCollection }}</dd>
                </div>
                <div>
                  <dt>Quality</dt>
                  <dd>{{ source.quality }}</dd>
                </div>
              </dl>
            </button>
          </div>
          <el-empty v-if="dataSources.length === 0" :image-size="72" description="No data sources" />
        </section>

        <section class="panel">
          <div class="panel-head">
            <h2>Recent Collection Events</h2>
            <span class="panel-meta">{{ recentEvents.length }} Events</span>
          </div>
          <el-timeline v-if="recentEvents.length > 0" class="event-timeline">
            <el-timeline-item
              v-for="event in recentEvents"
              :key="event.id"
              :timestamp="event.time"
              :type="event.timelineType"
            >
              <strong>{{ event.title }}</strong>
              <span>{{ event.eqpId }} / {{ event.context }}</span>
            </el-timeline-item>
          </el-timeline>
          <el-empty v-else :image-size="72" description="No recent collection events" />
        </section>
      </div>
    </section>

    <section v-show="activeTab === 'collection'" class="tab-panel">
      <section class="panel">
        <div class="panel-head">
          <h2>Current Collection Context</h2>
          <el-select v-model="selectedEquipmentId" class="equipment-select" placeholder="选择设备">
            <el-option v-for="eqp in equipmentList" :key="eqp.eqpId" :label="eqp.eqpId" :value="eqp.eqpId" />
          </el-select>
        </div>
        <div class="context-grid">
          <div v-for="item in collectionContext" :key="item.label" class="context-item">
            <span>{{ item.label }}</span>
            <strong>{{ item.value }}</strong>
          </div>
        </div>
      </section>

      <div class="content-grid content-grid--collection">
        <section class="panel">
          <div class="panel-head">
            <h2>Current Variables</h2>
            <div class="inline-filters">
              <el-input v-model="variableSearch" placeholder="Search Variable" clearable>
                <template #prefix>
                  <el-icon><Search /></el-icon>
                </template>
              </el-input>
              <el-select v-model="qualityFilter" placeholder="Quality">
                <el-option label="All" value="all" />
                <el-option label="GOOD" value="GOOD" />
                <el-option label="STALE" value="STALE" />
                <el-option label="BAD" value="BAD" />
                <el-option label="UNCERTAIN" value="UNCERTAIN" />
                <el-option label="OUT_OF_RANGE" value="OUT_OF_RANGE" />
              </el-select>
              <el-button type="primary" :loading="collecting" @click="collectCurrentRules">
                <el-icon><Refresh /></el-icon>
                S1F3 Collect
              </el-button>
            </div>
          </div>
          <el-table
            v-loading="trendLoading"
            :data="filteredVariables"
            height="360"
            stripe
            highlight-current-row
            row-key="key"
            @row-click="openVariableDetail"
          >
            <el-table-column prop="vid" label="SV ID" width="90" sortable />
            <el-table-column prop="name" label="Name" min-width="150" sortable show-overflow-tooltip />
            <el-table-column prop="value" label="Value" width="120" align="right" sortable />
            <el-table-column prop="unit" label="Unit" width="80" />
            <el-table-column prop="quality" label="Quality" width="140">
              <template #default="scope">
                <span class="status-badge" :class="'status-badge--' + qualityMeta(scope.row.quality).tone">
                  <span class="status-dot"></span>
                  {{ scope.row.quality }}
                </span>
              </template>
            </el-table-column>
            <el-table-column prop="timestamp" label="Timestamp" width="190" />
            <el-table-column prop="samplingRate" label="Sampling" width="110" align="right" />
            <el-table-column prop="source" label="Source" width="100" />
          </el-table>
          <el-empty v-if="!trendLoading && filteredVariables.length === 0" :image-size="72" description="No active variable data" />
        </section>

        <section class="panel">
          <div class="panel-head">
            <h2>Trend</h2>
            <el-segmented v-model="trendRangePreset" :options="trendPresets" @change="applyTrendPreset" />
          </div>
          <div class="trend-toolbar">
            <el-select
              v-model="selectedTrendKeys"
              multiple
              collapse-tags
              collapse-tags-tooltip
              :multiple-limit="6"
              placeholder="Select variables"
              @change="drawTrendChart"
            >
              <el-option
                v-for="item in currentVariables"
                :key="item.key"
                :label="item.name"
                :value="item.key"
              />
            </el-select>
            <el-date-picker
              v-model="trendTimeRange"
              type="datetimerange"
              range-separator="至"
              start-placeholder="开始时间"
              end-placeholder="结束时间"
              value-format="YYYY-MM-DD HH:mm:ss"
              @change="fetchTrendData"
            />
          </div>
          <div ref="trendChart" class="trend-chart"></div>
          <el-empty v-if="!trendLoading && trendSeries.length === 0" class="trend-empty" :image-size="72" description="No trend data in selected range" />
        </section>
      </div>
    </section>

    <section v-show="activeTab === 'plans'" class="tab-panel">
      <section class="panel">
        <div class="panel-head">
          <h2>Collection Plans</h2>
          <div class="toolbar">
            <el-input v-model="planSearch" placeholder="Search Plan / VID / Equipment" clearable>
              <template #prefix>
                <el-icon><Search /></el-icon>
              </template>
            </el-input>
            <el-button type="success" @click="openPlanEditor()">
              <el-icon><Plus /></el-icon>
              新增变量规则
            </el-button>
          </div>
        </div>
        <el-table
          v-loading="loading"
          :data="filteredPlans"
          height="430"
          stripe
          highlight-current-row
          row-key="key"
          @row-click="openPlanDetail"
        >
          <el-table-column prop="name" label="Plan Name" min-width="160" sortable show-overflow-tooltip />
          <el-table-column prop="equipment" label="Equipment" width="130" sortable />
          <el-table-column prop="trigger" label="Trigger" width="130" sortable />
          <el-table-column prop="samplingRate" label="Sampling Rate" width="140" align="right" sortable />
          <el-table-column prop="duration" label="Duration" width="110" align="right" />
          <el-table-column prop="variableCount" label="Variables" width="110" align="right" sortable />
          <el-table-column prop="version" label="Version" width="100" />
          <el-table-column label="Status" width="130">
            <template #default="scope">
              <span class="status-badge status-badge--info">
                <span class="status-dot"></span>
                {{ scope.row.status }}
              </span>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="160" align="center" fixed="right">
            <template #default="scope">
              <div class="icon-actions">
                <el-tooltip content="编辑第一条变量规则" placement="top">
                  <el-button type="primary" circle size="small" @click.stop="openPlanEditor(scope.row.rules[0])">
                    <el-icon><Edit /></el-icon>
                  </el-button>
                </el-tooltip>
                <el-tooltip content="Validate" placement="top">
                  <el-button circle size="small" @click.stop="validatePlan(scope.row)">
                    <el-icon><CircleCheck /></el-icon>
                  </el-button>
                </el-tooltip>
                <el-tooltip content="后端未提供 Plan 启停字段" placement="top">
                  <el-button circle size="small" disabled>
                    <el-icon><SwitchButton /></el-icon>
                  </el-button>
                </el-tooltip>
              </div>
            </template>
          </el-table-column>
        </el-table>
        <el-empty v-if="!loading && filteredPlans.length === 0" :image-size="72" description="No collection plan" />
      </section>
    </section>

    <section v-show="activeTab === 'quality'" class="tab-panel">
      <div class="kpi-grid kpi-grid--quality">
        <article v-for="item in qualityCards" :key="item.label" class="kpi-card" :class="'kpi-card--' + item.tone">
          <span>{{ item.label }}</span>
          <strong>{{ item.value }}</strong>
          <small>{{ item.note }}</small>
        </article>
      </div>

      <div class="content-grid content-grid--overview">
        <section class="panel">
          <div class="panel-head">
            <h2>Collection Health</h2>
            <span class="panel-meta">Derived from current rules and samples</span>
          </div>
          <div class="health-grid">
            <div v-for="item in collectionHealth" :key="item.label" class="health-item">
              <span>{{ item.label }}</span>
              <strong>{{ item.value }}</strong>
            </div>
          </div>
        </section>

        <section class="panel">
          <div class="panel-head">
            <h2>Data Quality Issues</h2>
            <span class="panel-meta">{{ qualityIssues.length }} Issues</span>
          </div>
          <el-table :data="qualityIssues" height="300" stripe>
            <el-table-column prop="timestamp" label="Time" width="190" />
            <el-table-column prop="eqpId" label="Equipment" width="120" />
            <el-table-column prop="vid" label="VID" width="90" />
            <el-table-column prop="name" label="Variable" min-width="150" show-overflow-tooltip />
            <el-table-column prop="quality" label="Quality" width="150">
              <template #default="scope">
                <span class="status-badge" :class="'status-badge--' + qualityMeta(scope.row.quality).tone">
                  <span class="status-dot"></span>
                  {{ scope.row.quality }}
                </span>
              </template>
            </el-table-column>
            <el-table-column prop="reason" label="Reason" min-width="150" show-overflow-tooltip />
          </el-table>
          <el-empty v-if="qualityIssues.length === 0" :image-size="72" description="No data quality issues" />
        </section>
      </div>
    </section>

    <section v-show="activeTab === 'history'" class="tab-panel">
      <section class="panel">
        <div class="panel-head panel-head--wrap">
          <h2>Collection History</h2>
          <div class="toolbar history-toolbar">
            <el-input v-model="historyFilters.keyword" placeholder="Equipment / Job / Carrier" clearable />
            <el-select v-model="historyFilters.status" placeholder="Status" clearable>
              <el-option label="全部" value="" />
              <el-option label="进行中" value="Running" />
              <el-option label="已完成" value="Completed" />
              <el-option label="失败" value="Error" />
            </el-select>
            <el-date-picker
              v-model="historyFilters.dateRange"
              type="datetimerange"
              range-separator="至"
              start-placeholder="开始时间"
              end-placeholder="结束时间"
              value-format="YYYY-MM-DD HH:mm:ss"
              clearable
            />
            <el-button plain @click="resetHistoryFilters">重置</el-button>
          </div>
        </div>
        <el-table
          v-loading="loading"
          :data="paginatedHistoryRows"
          height="430"
          stripe
          highlight-current-row
          row-key="id"
          @row-click="openHistoryDetail"
        >
          <el-table-column prop="date" label="Date" width="110" fixed sortable />
          <el-table-column prop="equipment" label="Equipment" width="130" fixed sortable />
          <el-table-column prop="job" label="Job / Lot" min-width="150" sortable show-overflow-tooltip />
          <el-table-column prop="recipe" label="Recipe" width="120" />
          <el-table-column prop="plan" label="Plan" min-width="140" show-overflow-tooltip />
          <el-table-column prop="trigger" label="Trigger" width="130" />
          <el-table-column prop="start" label="Start" width="170" />
          <el-table-column prop="end" label="End" width="170" />
          <el-table-column prop="duration" label="Duration" width="110" align="right" />
          <el-table-column prop="samples" label="Samples" width="110" align="right" />
          <el-table-column prop="quality" label="Data Quality" width="130" align="right" />
          <el-table-column label="Status" width="130" fixed="right">
            <template #default="scope">
              <span class="status-badge" :class="'status-badge--' + collectionStateMeta(scope.row.status).tone">
                <span class="status-dot"></span>
                {{ collectionStateMeta(scope.row.status).text }}
              </span>
            </template>
          </el-table-column>
        </el-table>
        <el-empty v-if="!loading && filteredHistoryRows.length === 0" :image-size="72" description="No historical records" />
        <div class="pagination-bar">
          <el-pagination
            v-model:current-page="historyPage.pageNo"
            v-model:page-size="historyPage.pageSize"
            :page-sizes="[10, 20, 50, 100]"
            :total="filteredHistoryRows.length"
            background
            layout="total, sizes, prev, pager, next, jumper"
          />
        </div>
      </section>
    </section>

    <el-drawer v-model="variableDrawerVisible" title="Variable Detail" size="420px">
      <div v-if="selectedVariable" class="detail-stack">
        <h3>{{ selectedVariable.name }}</h3>
        <div class="large-value">{{ selectedVariable.value }} <span>{{ selectedVariable.unit }}</span></div>
        <span class="status-badge" :class="'status-badge--' + qualityMeta(selectedVariable.quality).tone">
          <span class="status-dot"></span>
          {{ selectedVariable.quality }}
        </span>
        <el-descriptions :column="1" border>
          <el-descriptions-item label="SVID">{{ selectedVariable.vid }}</el-descriptions-item>
          <el-descriptions-item label="Source">{{ selectedVariable.source }}</el-descriptions-item>
          <el-descriptions-item label="Sampling">{{ selectedVariable.samplingRate }}</el-descriptions-item>
          <el-descriptions-item label="Last Update">{{ selectedVariable.timestamp }}</el-descriptions-item>
          <el-descriptions-item label="Min">{{ selectedVariable.stats.min }}</el-descriptions-item>
          <el-descriptions-item label="Max">{{ selectedVariable.stats.max }}</el-descriptions-item>
          <el-descriptions-item label="Average">{{ selectedVariable.stats.average }}</el-descriptions-item>
          <el-descriptions-item label="Std Dev">{{ selectedVariable.stats.stdDev }}</el-descriptions-item>
        </el-descriptions>
      </div>
    </el-drawer>

    <el-drawer v-model="planDrawerVisible" title="Collection Plan Detail" size="620px">
      <div v-if="selectedPlan" class="detail-stack">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="Plan Name">{{ selectedPlan.name }}</el-descriptions-item>
          <el-descriptions-item label="Description">N/A</el-descriptions-item>
          <el-descriptions-item label="Equipment">{{ selectedPlan.equipment }}</el-descriptions-item>
          <el-descriptions-item label="Chamber">N/A</el-descriptions-item>
          <el-descriptions-item label="Trigger">{{ selectedPlan.trigger }}</el-descriptions-item>
          <el-descriptions-item label="Sampling">{{ selectedPlan.samplingRate }}</el-descriptions-item>
          <el-descriptions-item label="Duration">{{ selectedPlan.duration }}</el-descriptions-item>
          <el-descriptions-item label="Version">N/A</el-descriptions-item>
        </el-descriptions>
        <div class="drawer-actions">
          <el-button type="primary" @click="openPlanEditor(selectedPlan.rules[0])">编辑变量规则</el-button>
          <el-button @click="validatePlan(selectedPlan)">Validate</el-button>
          <el-button disabled>Enable / Disable</el-button>
        </div>
        <el-table :data="selectedPlan.rules" height="300" stripe>
          <el-table-column prop="vid" label="SV ID" width="90" />
          <el-table-column label="Source" width="90">
            <template #default="scope">{{ vidTypeText(scope.row.vidType) }}</template>
          </el-table-column>
          <el-table-column prop="interval" label="Sampling" width="110" align="right">
            <template #default="scope">{{ formatSampling(scope.row.interval) }}</template>
          </el-table-column>
          <el-table-column prop="action" label="Action" min-width="150" show-overflow-tooltip />
          <el-table-column label="操作" width="110" align="center">
            <template #default="scope">
              <el-button type="primary" text @click="openPlanEditor(scope.row)">Edit</el-button>
              <el-button type="danger" text @click="deleteRule(scope.row)">Delete</el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>
    </el-drawer>

    <el-drawer v-model="historyDrawerVisible" title="Collection Timeline" size="460px">
      <div v-if="selectedHistory" class="detail-stack">
        <el-descriptions :column="1" border>
          <el-descriptions-item label="Equipment">{{ selectedHistory.equipment }}</el-descriptions-item>
          <el-descriptions-item label="Job / Lot">{{ selectedHistory.job }}</el-descriptions-item>
          <el-descriptions-item label="Carrier">{{ selectedHistory.carrier }}</el-descriptions-item>
          <el-descriptions-item label="Recipe">{{ selectedHistory.recipe }}</el-descriptions-item>
          <el-descriptions-item label="Plan">{{ selectedHistory.plan }}</el-descriptions-item>
          <el-descriptions-item label="Quality">{{ selectedHistory.quality }}</el-descriptions-item>
        </el-descriptions>
        <el-timeline>
          <el-timeline-item :timestamp="selectedHistory.start" type="primary">Job Start / Collection Context Created</el-timeline-item>
          <el-timeline-item
            v-for="event in selectedHistory.events"
            :key="event.id"
            :timestamp="event.time"
            :type="event.timelineType"
          >
            {{ event.title }} - {{ event.context }}
          </el-timeline-item>
          <el-timeline-item :timestamp="selectedHistory.end" :type="collectionStateMeta(selectedHistory.status).timelineType">
            {{ collectionStateMeta(selectedHistory.status).text }}
          </el-timeline-item>
        </el-timeline>
      </div>
    </el-drawer>

    <el-dialog v-model="ruleEditorVisible" :title="editingRule.id ? '编辑采集变量规则' : '新增采集变量规则'" width="680px">
      <el-form label-position="top" :model="editingRule">
        <div class="form-grid">
          <el-form-item label="设备号">
            <el-select v-model="editingRule.eqpId" placeholder="请选择设备">
              <el-option v-for="eqp in equipmentList" :key="eqp.eqpId" :label="eqp.eqpId" :value="eqp.eqpId" />
            </el-select>
          </el-form-item>
          <el-form-item label="VID">
            <el-input v-model="editingRule.vid" placeholder="VID" />
          </el-form-item>
          <el-form-item label="VID 类型">
            <el-select v-model="editingRule.vidType">
              <el-option label="DVID" :value="0" />
              <el-option label="SVID" :value="1" />
              <el-option label="ECID" :value="2" />
            </el-select>
          </el-form-item>
          <el-form-item label="采样间隔">
            <el-input-number v-model="editingRule.interval" :min="0" :step="1" controls-position="right" />
          </el-form-item>
          <el-form-item label="采集次数">
            <el-input-number v-model="editingRule.count" :min="0" :step="1" controls-position="right" />
          </el-form-item>
          <el-form-item label="触发方式">
            <el-select v-model="editingRule.triggerType">
              <el-option label="Continuous / EQP" :value="0" />
              <el-option label="CEID / EVENT" :value="1" />
            </el-select>
          </el-form-item>
          <el-form-item label="开始事件">
            <el-input v-model="editingRule.startEvent" placeholder="N/A" />
          </el-form-item>
          <el-form-item label="结束事件">
            <el-input v-model="editingRule.endEvent" placeholder="N/A" />
          </el-form-item>
          <el-form-item label="比对方式">
            <el-select v-model="editingRule.compareType">
              <el-option label="值比对" :value="0" />
              <el-option label="范围比对" :value="1" />
            </el-select>
          </el-form-item>
          <el-form-item label="比对值">
            <el-input v-model="editingRule.compareValue" />
          </el-form-item>
          <el-form-item label="最小值">
            <el-input v-model="editingRule.compareMin" />
          </el-form-item>
          <el-form-item label="最大值">
            <el-input v-model="editingRule.compareMax" />
          </el-form-item>
          <el-form-item label="允许次数">
            <el-input-number v-model="editingRule.limitTimes" :min="0" :step="1" controls-position="right" />
          </el-form-item>
          <el-form-item label="执行动作">
            <el-checkbox-group v-model="editingRule.actions">
              <el-checkbox :label="0">StopEqp</el-checkbox>
              <el-checkbox :label="1">HoldLot</el-checkbox>
            </el-checkbox-group>
          </el-form-item>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="ruleEditorVisible = false">取消</el-button>
        <el-button @click="validateRule(editingRule)">Validate</el-button>
        <el-button type="primary" :loading="savingRule" @click="saveRule">Save</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { nextTick } from 'vue'
import * as echarts from 'echarts'
import { GetList as GetDataRules, Add as AddDataRule, Edit as EditDataRule, Delete as DeleteDataRule } from '@/api/dataRule'
import { GetDashboardSnapshot } from '@/api/equipment'
import { GetList as GetRunHistory } from '@/api/runHistory'
import { GetList as GetExceptionList } from '@/api/Exception'
import { GetSvidTrend, GetSvidTrendwithAbnormal } from '@/api/vidValue'
import { CollectFdcValuesByRules, GetLatestFdcValues } from '@/api/fdcCollection'

const POLL_INTERVAL = 10000
const STALE_MS = 5 * 60 * 1000
const FDC_VIEW_STATE_KEY = 'fdc-console-view-state'

function readFdcViewState() {
  try {
    return JSON.parse(localStorage.getItem(FDC_VIEW_STATE_KEY)) || {}
  } catch {
    return {}
  }
}

function saveFdcViewState(state) {
  try {
    localStorage.setItem(FDC_VIEW_STATE_KEY, JSON.stringify(state))
  } catch {
    // Ignore storage failures so the console still works normally.
  }
}

function createTimeRangeByPresetValue(preset) {
  const now = new Date()
  const minutes = { '5m': 5, '30m': 30, '1h': 60, '8h': 480, '24h': 1440 }[preset] || 30
  return [formatDateTimeValue(new Date(now.getTime() - minutes * 60 * 1000)), formatDateTimeValue(now)]
}

function formatDateTimeValue(date) {
  const pad = value => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

export default {
  name: 'FdcConsole',
  props: {
    defaultTab: {
      type: String,
      default: 'overview'
    }
  },
  data() {
    const savedState = readFdcViewState()
    const trendRangePreset = savedState.trendRangePreset || '30m'
    return {
      activeTab: savedState.activeTab || this.defaultTab,
      loading: false,
      trendLoading: false,
      collecting: false,
      savingRule: false,
      errorMessage: '',
      lastRefreshAt: null,
      pollTimer: null,
      chart: null,
      equipmentList: [],
      dataRules: [],
      runHistories: [],
      exceptionList: [],
      trendSeries: [],
      latestValueRows: [],
      selectedEquipmentId: savedState.selectedEquipmentId || '',
      selectedTrendKeys: [],
      selectedVariable: null,
      selectedPlan: null,
      selectedHistory: null,
      variableDrawerVisible: false,
      planDrawerVisible: false,
      historyDrawerVisible: false,
      ruleEditorVisible: false,
      editingRule: this.getDefaultRule(),
      variableSearch: savedState.variableSearch || '',
      qualityFilter: savedState.qualityFilter || 'all',
      planSearch: savedState.planSearch || '',
      trendRangePreset,
      trendTimeRange: Array.isArray(savedState.trendTimeRange) && savedState.trendTimeRange.length === 2
        ? savedState.trendTimeRange
        : createTimeRangeByPresetValue(trendRangePreset),
      historyFilters: {
        keyword: savedState.historyFilters?.keyword || '',
        status: savedState.historyFilters?.status || '',
        dateRange: Array.isArray(savedState.historyFilters?.dateRange) ? savedState.historyFilters.dateRange : []
      },
      historyPage: {
        pageNo: savedState.historyPage?.pageNo || 1,
        pageSize: savedState.historyPage?.pageSize || 20
      },
      trendPresets: [
        { label: '5 min', value: '5m' },
        { label: '30 min', value: '30m' },
        { label: '1 hour', value: '1h' },
        { label: '8 hour', value: '8h' },
        { label: '24 hour', value: '24h' }
      ]
    }
  },
  computed: {
    lastRefreshText() {
      return this.lastRefreshAt ? this.formatDateTime(this.lastRefreshAt) : 'N/A'
    },
    connectedEquipmentCount() {
      return this.equipmentList.filter(item => Number(item.eqpStatus) === 1).length
    },
    activeCollectionCount() {
      return this.collectionRows.filter(row => row.status.text === 'Running').length
    },
    systemStatus() {
      if (this.errorMessage) {
        return { text: 'System Error', tone: 'danger' }
      }
      if (this.exceptionList.some(item => Number(item.state) === 0)) {
        return { text: 'Warning', tone: 'warning' }
      }
      if (this.connectedEquipmentCount > 0 && this.dataRules.length > 0) {
        return { text: 'System Normal', tone: 'success' }
      }
      return { text: 'Limited Data', tone: 'neutral' }
    },
    selectedEquipment() {
      return this.equipmentList.find(item => item.eqpId === this.selectedEquipmentId) || null
    },
    selectedRules() {
      return this.dataRules.filter(rule => !this.selectedEquipmentId || rule.eqpId === this.selectedEquipmentId)
    },
    selectedRunHistory() {
      return this.runHistories.find(item => item.eqpId === this.selectedEquipmentId && (Number(item.state) === 0 || !item.endTime)) || null
    },
    collectionContext() {
      const eqp = this.selectedEquipment
      const job = this.selectedRunHistory
      const port = this.findActivePort(eqp, job)
      const selectedPlan = this.collectionPlans.find(plan => plan.equipment === this.selectedEquipmentId)

      return [
        { label: 'Equipment', value: this.selectedEquipmentId || 'N/A' },
        { label: 'Job / Lot', value: job?.lotId || port?.lotId || 'N/A' },
        { label: 'Carrier', value: job?.carrierId || port?.carrierId || 'N/A' },
        { label: 'Recipe', value: 'N/A' },
        { label: 'Chamber', value: 'N/A' },
        { label: 'Collection Plan', value: selectedPlan?.name || this.derivePlanName(this.selectedEquipmentId) },
        { label: 'Trigger', value: selectedPlan?.trigger || 'N/A' },
        { label: 'Start Time', value: this.normalizeTime(job?.startTime) },
        { label: 'Duration', value: this.getDurationText(job?.startTime, job?.endTime) },
        { label: 'Sampling Rate', value: this.getEquipmentSampling(this.selectedEquipmentId) },
        { label: 'Current Data Rate', value: 'N/A' },
        { label: 'Samples', value: this.getEquipmentSampleCount(this.selectedEquipmentId) }
      ]
    },
    dataSources() {
      return this.equipmentList.map(eqp => {
        const eqpRules = this.dataRules.filter(rule => rule.eqpId === eqp.eqpId)
        const latest = this.getLatestPointForEquipment(eqp.eqpId)
        const quality = this.getEquipmentQuality(eqp.eqpId)
        return {
          eqpId: eqp.eqpId,
          eqpType: eqp.eqpType,
          connection: Number(eqp.eqpStatus) === 1
            ? { text: 'Connected', tone: 'success' }
            : { text: 'Disconnected', tone: 'danger' },
          variableCount: new Set(eqpRules.map(rule => rule.vid)).size,
          samplingRate: this.getEquipmentSampling(eqp.eqpId),
          lastCollection: latest ? latest.time : 'N/A',
          quality
        }
      })
    },
    collectionRows() {
      return this.equipmentList.map(eqp => {
        const eqpRules = this.dataRules.filter(rule => rule.eqpId === eqp.eqpId)
        const latest = this.getLatestPointForEquipment(eqp.eqpId)
        const job = this.runHistories.find(item => item.eqpId === eqp.eqpId && (Number(item.state) === 0 || !item.endTime))
        return {
          eqpId: eqp.eqpId,
          status: this.getCollectionStatus(eqp, eqpRules, latest),
          plan: this.derivePlanName(eqp.eqpId),
          job: job?.lotId || 'N/A',
          recipe: 'N/A',
          sampling: this.getEquipmentSampling(eqp.eqpId),
          samples: this.getEquipmentSampleCount(eqp.eqpId)
        }
      })
    },
    pipelineRows() {
      return [
        {
          name: 'Equipment',
          text: this.connectedEquipmentCount > 0 ? 'Normal' : 'Offline',
          tone: this.connectedEquipmentCount > 0 ? 'success' : 'danger'
        },
        {
          name: 'SECS/GEM',
          text: this.equipmentList.some(item => Number(item.eapStatus) === 1) ? 'Normal' : 'Unknown',
          tone: this.equipmentList.some(item => Number(item.eapStatus) === 1) ? 'success' : 'neutral'
        },
        {
          name: 'FDC Collector',
          text: this.dataRules.length > 0 ? 'Configured' : 'No Plan',
          tone: this.dataRules.length > 0 ? 'info' : 'neutral'
        },
        { name: 'Data Buffer', text: 'N/A', tone: 'neutral' },
        {
          name: 'Storage',
          text: this.trendSeries.length > 0 ? 'Readable' : 'Unknown',
          tone: this.trendSeries.length > 0 ? 'success' : 'neutral'
        }
      ]
    },
    currentVariables() {
      return this.trendSeries.map(series => {
        const points = Array.isArray(series.points) ? series.points : []
        const latest = this.getLatestValueForSeries(series) || points[points.length - 1]
        const rule = this.findRule(series.eqpId || this.selectedEquipmentId, series.vid)
        const values = points.map(point => Number(point.value)).filter(value => Number.isFinite(value))
        const stats = this.calcStats(values)
        const quality = latest ? this.resolveQuality(latest, rule) : 'MISSING'

        return {
          key: this.getSeriesKey(series),
          eqpId: series.eqpId || this.selectedEquipmentId,
          vid: series.vid,
          name: series.name || `VID-${series.vid}`,
          value: latest ? this.formatNumber(latest.value) : 'N/A',
          unit: 'N/A',
          quality,
          timestamp: latest ? this.normalizeTime(latest.time) : 'N/A',
          samplingRate: rule ? this.formatSampling(rule.interval) : 'N/A',
          source: rule ? this.vidTypeText(rule.vidType) : 'VIDValue',
          stats
        }
      })
    },
    filteredVariables() {
      const keyword = this.variableSearch.trim().toLowerCase()
      return this.currentVariables.filter(item => {
        const matchKeyword = !keyword ||
          String(item.vid).includes(keyword) ||
          String(item.name || '').toLowerCase().includes(keyword)
        const matchQuality = this.qualityFilter === 'all' || item.quality === this.qualityFilter
        return matchKeyword && matchQuality
      })
    },
    collectionPlans() {
      const groups = new Map()
      this.dataRules.forEach(rule => {
        const key = [
          rule.eqpId || 'N/A',
          rule.triggerType ?? 'N/A',
          rule.startEvent || '',
          rule.endEvent || '',
          rule.interval || 0
        ].join('|')

        if (!groups.has(key)) {
          groups.set(key, {
            key,
            name: this.getPlanName(rule),
            equipment: rule.eqpId || 'N/A',
            trigger: this.triggerText(rule),
            samplingRate: this.formatSampling(rule.interval),
            duration: this.formatDurationFromCount(rule.interval, rule.count),
            variableCount: 0,
            version: 'N/A',
            status: 'Configured',
            rules: []
          })
        }
        groups.get(key).rules.push(rule)
      })

      return Array.from(groups.values()).map(plan => ({
        ...plan,
        variableCount: new Set(plan.rules.map(rule => rule.vid)).size
      }))
    },
    filteredPlans() {
      const keyword = this.planSearch.trim().toLowerCase()
      if (!keyword) return this.collectionPlans
      return this.collectionPlans.filter(plan =>
        [plan.name, plan.equipment, plan.trigger, ...plan.rules.map(rule => rule.vid)]
          .some(value => String(value || '').toLowerCase().includes(keyword))
      )
    },
    qualityOverview() {
      const total = this.qualityStats.total
      if (total === 0) {
        return { label: 'N/A', tone: 'neutral', good: 0, total: 0 }
      }
      const percent = (this.qualityStats.good / total) * 100
      return {
        label: percent.toFixed(1) + '%',
        tone: percent >= 99 ? 'success' : percent >= 95 ? 'warning' : 'danger',
        good: this.qualityStats.good,
        total
      }
    },
    qualityStats() {
      const stats = {
        total: 0,
        good: 0,
        bad: 0,
        uncertain: 0,
        stale: 0,
        outOfRange: 0
      }

      this.trendSeries.forEach(series => {
        const rule = this.findRule(series.eqpId || this.selectedEquipmentId, series.vid)
        ;(series.points || []).forEach(point => {
          const quality = this.resolveQuality(point, rule)
          stats.total += 1
          if (quality === 'GOOD') stats.good += 1
          else if (quality === 'BAD' || quality === 'INVALID') stats.bad += 1
          else if (quality === 'UNCERTAIN') stats.uncertain += 1
          else if (quality === 'STALE') stats.stale += 1
          else if (quality === 'OUT_OF_RANGE') stats.outOfRange += 1
        })
      })
      return stats
    },
    qualityCards() {
      return [
        { label: 'Total Samples', value: this.qualityStats.total, tone: 'info', note: '当前趋势时间窗' },
        { label: 'Good', value: this.qualityStats.good, tone: 'success', note: 'ABNORMAL_FLAG = 0' },
        { label: 'Missing', value: 'N/A', tone: 'neutral', note: '后端未提供缺失样本计数' },
        { label: 'Invalid / Bad', value: this.qualityStats.bad, tone: 'danger', note: 'ZeroValue 等异常标识' },
        { label: 'Stale', value: this.qualityStats.stale, tone: 'warning', note: 'SameAsPrevious 标识' },
        { label: 'Out Of Range', value: this.qualityStats.outOfRange, tone: 'danger', note: '基于规则阈值推断' },
        { label: 'Data Quality', value: this.qualityOverview.label, tone: this.qualityOverview.tone, note: 'Good / Total' }
      ]
    },
    qualityIssues() {
      const rows = []
      this.trendSeries.forEach(series => {
        const rule = this.findRule(series.eqpId || this.selectedEquipmentId, series.vid)
        ;(series.points || []).forEach(point => {
          const quality = this.resolveQuality(point, rule)
          if (quality === 'GOOD') return
          rows.push({
            id: `${this.getSeriesKey(series)}-${point.time}`,
            eqpId: series.eqpId || this.selectedEquipmentId,
            vid: series.vid,
            name: series.name || `VID-${series.vid}`,
            timestamp: this.normalizeTime(point.time),
            quality,
            reason: this.qualityReason(point, rule, quality)
          })
        })
      })
      return rows.slice(-200).reverse()
    },
    collectionHealth() {
      const selectedRuleIntervals = this.selectedRules.map(rule => Number(rule.interval)).filter(value => Number.isFinite(value) && value > 0)
      const expected = selectedRuleIntervals.length ? Math.min(...selectedRuleIntervals) : null
      return [
        { label: 'Expected Sampling', value: expected ? this.formatSampling(expected) : 'N/A' },
        { label: 'Actual Sampling', value: this.getActualSamplingText() },
        { label: 'Sampling Drift', value: 'N/A' },
        { label: 'Data Loss', value: 'N/A' },
        { label: 'Timestamp Drift', value: 'N/A' },
        { label: 'Communication Loss', value: 'N/A' },
        { label: 'Last Successful Sample', value: this.getLatestPointForEquipment(this.selectedEquipmentId)?.time || 'N/A' }
      ]
    },
    historyRows() {
      return this.runHistories.map(row => {
        const plan = this.collectionPlans.find(item => item.equipment === row.eqpId)
        const status = Number(row.state) === 0 || !row.endTime ? 'Running' : Number(row.state) === 1 ? 'Error' : 'Completed'
        return {
          id: row.id,
          raw: row,
          date: this.normalizeTime(row.startTime).slice(0, 10) || 'N/A',
          equipment: row.eqpId || 'N/A',
          job: row.lotId || 'N/A',
          carrier: row.carrierId || 'N/A',
          recipe: 'N/A',
          plan: plan?.name || this.derivePlanName(row.eqpId),
          trigger: plan?.trigger || 'N/A',
          start: this.normalizeTime(row.startTime),
          end: this.normalizeTime(row.endTime),
          duration: this.getDurationText(row.startTime, row.endTime),
          samples: this.getEquipmentSampleCount(row.eqpId),
          quality: this.getEquipmentQuality(row.eqpId),
          status,
          events: this.getEventsForHistory(row)
        }
      })
    },
    filteredHistoryRows() {
      const keyword = this.historyFilters.keyword.trim().toLowerCase()
      const [startText, endText] = this.historyFilters.dateRange || []
      const start = startText ? Date.parse(startText) : null
      const end = endText ? Date.parse(endText) : null
      return this.historyRows.filter(row => {
        const matchKeyword = !keyword || [row.equipment, row.job, row.carrier, row.plan]
          .some(value => String(value || '').toLowerCase().includes(keyword))
        const matchStatus = !this.historyFilters.status || row.status === this.historyFilters.status
        const time = Date.parse(row.start)
        const matchTime = (!start && !end) || (Number.isFinite(time) && (!start || time >= start) && (!end || time <= end))
        return matchKeyword && matchStatus && matchTime
      })
    },
    paginatedHistoryRows() {
      const start = (this.historyPage.pageNo - 1) * this.historyPage.pageSize
      const end = this.historyPage.pageNo * this.historyPage.pageSize
      return this.filteredHistoryRows.slice(start, end)
    },
    recentEvents() {
      const rows = []
      this.runHistories.forEach(row => {
        rows.push({
          id: `run-${row.id}-start`,
          time: this.normalizeTime(row.startTime),
          sortTime: Date.parse(this.normalizeTime(row.startTime)) || 0,
          title: 'Collection Started',
          eqpId: row.eqpId || 'N/A',
          context: row.lotId || 'N/A',
          timelineType: 'primary'
        })
        if (row.endTime) {
          rows.push({
            id: `run-${row.id}-end`,
            time: this.normalizeTime(row.endTime),
            sortTime: Date.parse(this.normalizeTime(row.endTime)) || 0,
            title: 'Collection Completed',
            eqpId: row.eqpId || 'N/A',
            context: row.lotId || 'N/A',
            timelineType: 'success'
          })
        }
      })
      this.exceptionList.forEach(row => {
        rows.push({
          id: `exception-${row.id}`,
          time: this.normalizeTime(row.errorTime),
          sortTime: Date.parse(this.normalizeTime(row.errorTime)) || 0,
          title: Number(row.state) === 0 ? 'Warning' : 'Warning Closed',
          eqpId: row.eqpId || 'N/A',
          context: row.content || 'N/A',
          timelineType: Number(row.state) === 0 ? 'warning' : 'success'
        })
      })
      return rows.sort((a, b) => b.sortTime - a.sortTime).slice(0, 10)
    }
  },
  watch: {
    selectedEquipmentId() {
      this.persistViewState()
      if (!this.loading) {
        this.fetchTrendData()
      }
    },
    activeTab(value) {
      this.persistViewState()
      if (value === 'collection' || value === 'quality') {
        nextTick(() => this.drawTrendChart())
      }
    },
    variableSearch() {
      this.persistViewState()
    },
    qualityFilter() {
      this.persistViewState()
    },
    planSearch() {
      this.persistViewState()
    },
    trendRangePreset() {
      this.persistViewState()
    },
    trendTimeRange: {
      handler() {
        this.persistViewState()
      },
      deep: true
    },
    historyFilters: {
      handler() {
        this.persistViewState()
      },
      deep: true
    },
    historyPage: {
      handler() {
        this.persistViewState()
      },
      deep: true
    }
  },
  created() {
    this.reloadAll()
  },
  mounted() {
    this.startPolling()
  },
  activated() {
    this.startPolling()
    nextTick(() => this.drawTrendChart())
  },
  deactivated() {
    this.stopPolling()
  },
  beforeUnmount() {
    this.stopPolling()
    this.disposeChart()
  },
  methods: {
    persistViewState() {
      saveFdcViewState({
        activeTab: this.activeTab,
        selectedEquipmentId: this.selectedEquipmentId,
        variableSearch: this.variableSearch,
        qualityFilter: this.qualityFilter,
        planSearch: this.planSearch,
        trendRangePreset: this.trendRangePreset,
        trendTimeRange: this.trendTimeRange,
        historyFilters: this.historyFilters,
        historyPage: this.historyPage
      })
    },
    async reloadAll(options = {}) {
      if (!options.silent) {
        this.loading = true
      }
      this.errorMessage = ''
      try {
        const [rulesRes, snapshotRes, historyRes, exceptionRes, latestRes] = await Promise.all([
          GetDataRules({}),
          GetDashboardSnapshot({}),
          GetRunHistory({}),
          GetExceptionList({}),
          GetLatestFdcValues({ limit: 1000 }).catch(() => ({ data: { list: [] } }))
        ])

        this.dataRules = this.extractList(rulesRes)
        const snapshot = snapshotRes?.data || {}
        this.equipmentList = Array.isArray(snapshot.equipments?.list) ? snapshot.equipments.list : []
        this.runHistories = this.extractList(historyRes)
        this.exceptionList = Array.isArray(snapshot.activeAlarms?.list)
          ? snapshot.activeAlarms.list
          : this.extractList(exceptionRes)
        this.latestValueRows = this.extractList(latestRes)

        if (this.equipmentList.length > 0 && !this.equipmentList.some(item => item.eqpId === this.selectedEquipmentId)) {
          this.selectedEquipmentId = this.equipmentList[0].eqpId
        }
        this.lastRefreshAt = new Date()
        await this.fetchTrendData({ silent: true })
      } catch (error) {
        this.errorMessage = error?.message || 'Unable to load FDC data.'
      } finally {
        this.loading = false
      }
    },
    async fetchTrendData(options = {}) {
      if (!this.trendTimeRange || this.trendTimeRange.length !== 2) {
        this.trendSeries = []
        this.drawTrendChart()
        return
      }

      if (!options.silent) {
        this.trendLoading = true
      }

      try {
        const params = {
          eqpId: this.selectedEquipmentId || '',
          startTime: this.trendTimeRange[0],
          endTime: this.trendTimeRange[1],
          maxPointsPerSeries: 800
        }
        const res = this.qualityFilter !== 'all' && this.qualityFilter !== 'OUT_OF_RANGE' && this.qualityFilter !== 'MISSING'
          ? await GetSvidTrendwithAbnormal({ ...params, AbnormalFlag: this.qualityToAbnormalFlag(this.qualityFilter) })
          : await GetSvidTrend(params)
        const list = Array.isArray(res?.data) ? res.data : []
        this.trendSeries = list.map(item => ({
          ...item,
          eqpId: this.selectedEquipmentId
        }))
        this.syncTrendSelection()
        await nextTick()
        this.drawTrendChart()
      } catch (error) {
        this.trendSeries = []
        this.errorMessage = error?.message || 'Unable to load trend data.'
      } finally {
        this.trendLoading = false
      }
    },
    startPolling() {
      if (this.pollTimer) return
      this.pollTimer = setInterval(() => {
        this.reloadAll({ silent: true })
      }, POLL_INTERVAL)
    },
    stopPolling() {
      if (!this.pollTimer) return
      clearInterval(this.pollTimer)
      this.pollTimer = null
    },
    handleTabChange(tab) {
      if (tab === 'collection' || tab === 'quality') {
        nextTick(() => this.drawTrendChart())
      }
    },
    selectEquipmentFromRow(row) {
      this.selectedEquipmentId = row.eqpId
      this.activeTab = 'collection'
    },
    applyTrendPreset() {
      this.trendTimeRange = this.createTimeRangeByPreset(this.trendRangePreset)
      this.fetchTrendData()
    },
    syncTrendSelection() {
      const keys = this.currentVariables.map(item => item.key)
      this.selectedTrendKeys = this.selectedTrendKeys.filter(key => keys.includes(key))
      if (this.selectedTrendKeys.length === 0) {
        this.selectedTrendKeys = keys.slice(0, 4)
      }
    },
    drawTrendChart() {
      const el = this.$refs.trendChart
      if (!el || this.activeTab !== 'collection') return
      try {
        if (!this.chart) {
          this.chart = echarts.init(el)
        }

        const selectedKeys = new Set(this.selectedTrendKeys)
        const series = this.trendSeries
          .filter(item => selectedKeys.has(this.getSeriesKey(item)))
          .map(item => ({
            name: item.name || `VID-${item.vid}`,
            type: 'line',
            showSymbol: false,
            smooth: true,
            emphasis: { focus: 'series' },
            lineStyle: { width: 2 },
            data: (item.points || [])
              .map(point => [this.normalizeTime(point.time), Number(point.value)])
              .filter(point => point[0] !== 'N/A' && Number.isFinite(point[1]))
          }))

        if (series.length === 0) {
          this.chart.clear()
          return
        }

        this.chart.setOption({
          animation: false,
          backgroundColor: 'transparent',
          tooltip: { trigger: 'axis', axisPointer: { type: 'line' } },
          legend: { type: 'scroll', top: 0, icon: 'circle', itemWidth: 10, itemHeight: 10 },
          grid: { left: 50, right: 22, top: 46, bottom: 48, containLabel: true },
          dataZoom: [
            { type: 'inside' },
            { type: 'slider', height: 16, bottom: 8 }
          ],
          xAxis: {
            type: 'time',
            axisLine: { lineStyle: { color: '#dbe4ef' } },
            axisLabel: { color: '#6a7688' },
            splitLine: { show: false }
          },
          yAxis: {
            type: 'value',
            scale: true,
            axisLine: { lineStyle: { color: '#dbe4ef' } },
            axisLabel: { color: '#6a7688' },
            splitLine: { lineStyle: { color: '#edf2f7' } }
          },
          series
        }, true)
        this.chart.resize()
      } catch (error) {
        console.warn('Trend chart render failed', error)
      }
    },
    disposeChart() {
      if (!this.chart) return
      this.chart.dispose()
      this.chart = null
    },
    openVariableDetail(row) {
      this.selectedVariable = row
      this.variableDrawerVisible = true
    },
    openPlanDetail(row) {
      this.selectedPlan = row
      this.planDrawerVisible = true
    },
    openHistoryDetail(row) {
      this.selectedHistory = row
      this.historyDrawerVisible = true
    },
    openPlanEditor(rule) {
      this.editingRule = rule ? { ...this.getDefaultRule(), ...rule, actions: [...(rule.actions || [])] } : this.getDefaultRule()
      this.ruleEditorVisible = true
    },
    validateRule(rule) {
      if (!rule.eqpId || !rule.vid || rule.interval === null || rule.interval === undefined) {
        this.$message.warning('设备号、VID、采样间隔不能为空')
        return false
      }
      this.$message.success('规则字段校验通过')
      return true
    },
    validatePlan(plan) {
      if (!plan || !plan.rules || plan.rules.length === 0) {
        this.$message.warning('采集计划没有变量规则')
        return false
      }
      this.$message.success('采集计划可用，启停状态需后端提供 Plan Status 字段')
      return true
    },
    async saveRule() {
      if (!this.validateRule(this.editingRule)) return
      this.savingRule = true
      const payload = {
        ...this.editingRule,
        action: (this.editingRule.actions || []).map(this.actionText).join(';') + ((this.editingRule.actions || []).length ? ';' : '')
      }
      try {
        const res = payload.id ? await EditDataRule(payload) : await AddDataRule(payload)
        if (this.isOk(res)) {
          this.$message.success(payload.id ? '采集规则已更新' : '采集规则已新增')
          this.ruleEditorVisible = false
          await this.reloadAll()
        } else {
          this.$message.error(res?.message || res?.MESSAGE || '保存失败')
        }
      } finally {
        this.savingRule = false
      }
    },
    deleteRule(rule) {
      this.$confirm(`确定删除 VID ${rule.vid} 的采集规则吗？`, '删除确认', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(async () => {
        const res = await DeleteDataRule([rule.id])
        if (this.isOk(res)) {
          this.$message.success('删除成功')
          await this.reloadAll()
        } else {
          this.$message.error(res?.message || res?.MESSAGE || '删除失败')
        }
      }).catch(() => {})
    },
    async collectCurrentRules() {
      if (!this.selectedEquipmentId) {
        this.$message.warning('Please select equipment first.')
        return
      }

      this.collecting = true
      try {
        const res = await CollectFdcValuesByRules({
          EqpId: this.selectedEquipmentId,
          SaveToHistory: true,
          ChunkSize: 120
        })

        if (this.isOk(res)) {
          const success = res?.data?.success ?? 0
          const failed = res?.data?.failed ?? 0
          this.$message.success(`S1F3 collected ${success} values, ${failed} failed.`)
          await this.reloadAll({ silent: true })
        } else {
          this.$message.error(res?.MESSAGE || res?.message || 'S1F3 collect failed.')
        }
      } finally {
        this.collecting = false
      }
    },
    resetHistoryFilters() {
      this.historyFilters.keyword = ''
      this.historyFilters.status = ''
      this.historyFilters.dateRange = []
      this.historyPage.pageNo = 1
    },
    extractList(res) {
      return res && res.data && Array.isArray(res.data.list) ? res.data.list : []
    },
    isOk(res) {
      return res?.return === 'OK' || res?.Return === 'OK'
    },
    getDefaultRule() {
      return {
        id: null,
        vid: '',
        vidType: 1,
        interval: 1,
        count: 0,
        eqpId: '',
        triggerType: 0,
        startEvent: '',
        endEvent: '',
        compareType: 1,
        compareValue: '',
        compareMin: '',
        compareMax: '',
        limitTimes: 0,
        actions: [],
        action: ''
      }
    },
    getPlanName(rule) {
      return `${rule.eqpId || 'N/A'}-${this.triggerText(rule)}-${this.formatSampling(rule.interval)}`
    },
    derivePlanName(eqpId) {
      const rule = this.dataRules.find(item => item.eqpId === eqpId)
      return rule ? this.getPlanName(rule) : 'N/A'
    },
    triggerText(rule) {
      if (Number(rule.triggerType) === 1) {
        return rule.startEvent ? `CEID ${rule.startEvent}` : 'CEID / EVENT'
      }
      return 'Continuous / EQP'
    },
    vidTypeText(type) {
      const map = { 0: 'DVID', 1: 'SVID', 2: 'ECID' }
      return map[Number(type)] || 'VID'
    },
    formatSampling(value) {
      const num = Number(value)
      if (!Number.isFinite(num) || num <= 0) return 'N/A'
      return num >= 1 ? `${num} sec` : `${num * 1000} ms`
    },
    formatDurationFromCount(interval, count) {
      const seconds = Number(interval) * Number(count)
      if (!Number.isFinite(seconds) || seconds <= 0) return 'Continuous'
      return this.formatDurationSeconds(seconds)
    },
    actionText(action) {
      return Number(action) === 0 ? 'StopEqp' : 'HoldLot'
    },
    findRule(eqpId, vid) {
      return this.dataRules.find(rule => rule.eqpId === eqpId && String(rule.vid) === String(vid)) ||
        this.dataRules.find(rule => String(rule.vid) === String(vid))
    },
    findActivePort(eqp, job) {
      const ports = eqp && Array.isArray(eqp.portInfos) ? eqp.portInfos : []
      if (job) {
        return ports.find(port => Number(port.portId) === Number(job.portId)) || null
      }
      return ports.find(port => port.carrierId || port.lotId) || null
    },
    getCollectionStatus(eqp, rules, latest) {
      if (!eqp || Number(eqp.eqpStatus) !== 1) return { text: 'Offline', tone: 'danger' }
      if (!rules || rules.length === 0) return { text: 'Idle', tone: 'neutral' }
      if (!latest) return { text: 'Partial', tone: 'warning' }
      const time = Date.parse(this.normalizeTime(latest.time))
      if (Number.isFinite(time) && Date.now() - time > STALE_MS) return { text: 'Warning', tone: 'warning' }
      return { text: 'Running', tone: 'success' }
    },
    collectionStateMeta(status) {
      const map = {
        Running: { text: '进行中', tone: 'info', timelineType: 'primary' },
        Completed: { text: '已完成', tone: 'success', timelineType: 'success' },
        Error: { text: '失败', tone: 'danger', timelineType: 'danger' }
      }
      return map[status] || { text: 'Unknown', tone: 'neutral', timelineType: 'info' }
    },
    qualityMeta(status) {
      const map = {
        GOOD: { tone: 'success' },
        BAD: { tone: 'danger' },
        UNCERTAIN: { tone: 'warning' },
        STALE: { tone: 'warning' },
        MISSING: { tone: 'neutral' },
        INVALID: { tone: 'danger' },
        OUT_OF_RANGE: { tone: 'danger' }
      }
      return map[status] || { tone: 'neutral' }
    },
    resolveQuality(point, rule) {
      if (!point) return 'MISSING'
      if (rule && this.isOutOfRange(point.value, rule)) return 'OUT_OF_RANGE'
      const flag = Number(point.abnormalFlag)
      if ((flag & 1) === 1) return 'STALE'
      if ((flag & 2) === 2) return 'BAD'
      if ((flag & 4) === 4) return 'UNCERTAIN'
      return 'GOOD'
    },
    qualityReason(point, rule, quality) {
      if (quality === 'OUT_OF_RANGE') return `超过采集规则阈值 ${rule?.compareMin || 'N/A'} ~ ${rule?.compareMax || 'N/A'}`
      if (quality === 'STALE') return 'ABNORMAL_FLAG: SameAsPrevious'
      if (quality === 'BAD') return 'ABNORMAL_FLAG: ZeroValue'
      if (quality === 'UNCERTAIN') return 'ABNORMAL_FLAG: FirstValue'
      return 'N/A'
    },
    isOutOfRange(value, rule) {
      if (!rule || Number(rule.compareType) !== 1) return false
      const num = Number(value)
      const min = Number(rule.compareMin)
      const max = Number(rule.compareMax)
      if (!Number.isFinite(num)) return false
      if (Number.isFinite(min) && num < min) return true
      if (Number.isFinite(max) && num > max) return true
      return false
    },
    qualityToAbnormalFlag(status) {
      const map = { GOOD: 0, STALE: 1, BAD: 2, UNCERTAIN: 4 }
      return map[status] ?? -1
    },
    getSeriesKey(series) {
      return `${series.eqpId || this.selectedEquipmentId}-${series.vid}-${series.name || ''}`
    },
    getLatestValueForSeries(series) {
      const eqpId = series.eqpId || this.selectedEquipmentId
      return this.latestValueRows.find(item =>
        (!eqpId || item.eqpId === eqpId) && String(item.vid) === String(series.vid)
      ) || null
    },
    getLatestPointForEquipment(eqpId) {
      const latestLiveRow = this.latestValueRows
        .filter(item => !eqpId || item.eqpId === eqpId)
        .reduce((latest, item) => {
          if (!latest || Date.parse(this.normalizeTime(item.time)) > Date.parse(this.normalizeTime(latest.time))) {
            return item
          }
          return latest
        }, null)

      if (latestLiveRow) {
        return latestLiveRow
      }

      let latest = null
      this.trendSeries
        .filter(series => !eqpId || series.eqpId === eqpId)
        .forEach(series => {
          (series.points || []).forEach(point => {
            if (!latest || Date.parse(this.normalizeTime(point.time)) > Date.parse(this.normalizeTime(latest.time))) {
              latest = point
            }
          })
        })
      return latest
    },
    getEquipmentSampleCount(eqpId) {
      const count = this.trendSeries
        .filter(series => !eqpId || series.eqpId === eqpId)
        .reduce((sum, series) => sum + (Array.isArray(series.points) ? series.points.length : 0), 0)
      return count || 'N/A'
    },
    getEquipmentSampling(eqpId) {
      const intervals = this.dataRules
        .filter(rule => rule.eqpId === eqpId)
        .map(rule => Number(rule.interval))
        .filter(value => Number.isFinite(value) && value > 0)
      return intervals.length ? this.formatSampling(Math.min(...intervals)) : 'N/A'
    },
    getEquipmentQuality(eqpId) {
      const series = this.trendSeries.filter(item => !eqpId || item.eqpId === eqpId)
      let total = 0
      let good = 0
      series.forEach(item => {
        const rule = this.findRule(item.eqpId || eqpId, item.vid)
        ;(item.points || []).forEach(point => {
          total += 1
          if (this.resolveQuality(point, rule) === 'GOOD') good += 1
        })
      })
      return total ? ((good / total) * 100).toFixed(1) + '%' : 'N/A'
    },
    getActualSamplingText() {
      const points = this.trendSeries[0]?.points || []
      if (points.length < 2) return 'N/A'
      const last = Date.parse(this.normalizeTime(points[points.length - 1].time))
      const prev = Date.parse(this.normalizeTime(points[points.length - 2].time))
      if (!Number.isFinite(last) || !Number.isFinite(prev)) return 'N/A'
      return ((last - prev) / 1000).toFixed(2) + ' sec'
    },
    getEventsForHistory(row) {
      return this.exceptionList
        .filter(item => item.eqpId === row.eqpId && (!row.lotId || item.lotId === row.lotId))
        .map(item => ({
          id: item.id,
          time: this.normalizeTime(item.errorTime),
          title: Number(item.state) === 0 ? 'Alarm / Exception' : 'Exception Closed',
          context: item.content || 'N/A',
          timelineType: Number(item.state) === 0 ? 'warning' : 'success'
        }))
        .slice(0, 8)
    },
    calcStats(values) {
      if (!values.length) {
        return { min: 'N/A', max: 'N/A', average: 'N/A', stdDev: 'N/A' }
      }
      const min = Math.min(...values)
      const max = Math.max(...values)
      const avg = values.reduce((sum, item) => sum + item, 0) / values.length
      const variance = values.reduce((sum, item) => sum + Math.pow(item - avg, 2), 0) / values.length
      return {
        min: this.formatNumber(min),
        max: this.formatNumber(max),
        average: this.formatNumber(avg),
        stdDev: this.formatNumber(Math.sqrt(variance))
      }
    },
    createTimeRangeByPreset(preset) {
      const now = new Date()
      const minutes = { '5m': 5, '30m': 30, '1h': 60, '8h': 480, '24h': 1440 }[preset] || 30
      return [this.formatDateTime(new Date(now.getTime() - minutes * 60 * 1000)), this.formatDateTime(now)]
    },
    normalizeTime(value) {
      if (!value) return 'N/A'
      return String(value).replace(/\s{2,}/g, ' ').replace(/:(\d{3})$/, '.$1')
    },
    formatDateTime(date) {
      const pad = value => String(value).padStart(2, '0')
      return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
    },
    getDurationText(start, end) {
      const startTime = Date.parse(this.normalizeTime(start))
      const endTime = end ? Date.parse(this.normalizeTime(end)) : Date.now()
      if (!Number.isFinite(startTime) || !Number.isFinite(endTime)) return 'N/A'
      return this.formatDurationSeconds(Math.max(Math.floor((endTime - startTime) / 1000), 0))
    },
    formatDurationSeconds(seconds) {
      if (seconds < 60) return `${seconds}s`
      const minutes = Math.floor(seconds / 60)
      if (minutes < 60) return `${minutes}m ${seconds % 60}s`
      const hours = Math.floor(minutes / 60)
      return `${hours}h ${minutes % 60}m`
    },
    formatNumber(value) {
      const num = Number(value)
      if (!Number.isFinite(num)) return 'N/A'
      return Math.abs(num) >= 100 ? num.toFixed(0) : num.toFixed(3).replace(/\.?0+$/, '')
    }
  }
}
</script>

<style lang="scss" scoped>
.fdc-console {
  min-height: calc(100vh - 64px);
  max-width: 1760px;
  margin: 0 auto;
  padding: 18px 20px 28px;
  background: #f5f7fa;
  color: #182433;
  text-align: left;
  box-sizing: border-box;
}

.page-header,
.filter-panel,
.panel,
.kpi-card,
.error-panel {
  border: 1px solid #dce3ec;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.04);
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 18px;
  margin-bottom: 12px;
}

.page-header h1,
.panel-head h2 {
  margin: 0;
  color: #152235;
  letter-spacing: 0;
}

.page-header h1 {
  font-size: 20px;
  line-height: 1.2;
}

.page-header p,
.panel-meta,
.last-update,
.kpi-card small {
  color: #6a7688;
  font-size: 12px;
}

.page-header p {
  margin: 4px 0 0;
  font-weight: 700;
}

.header-actions,
.toolbar,
.inline-filters,
.icon-actions,
.drawer-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.error-panel {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
  padding: 10px 12px;
  color: #b42318;
  background: #fff7f5;
  border-color: #ffd0ca;
}

.error-panel div {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.fdc-tabs {
  padding: 2px 14px 0;
  margin-bottom: 12px;
  border: 1px solid #dce3ec;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.96);
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.03);
}

.tab-panel {
  display: grid;
  gap: 12px;
}

.kpi-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(160px, 1fr));
  gap: 14px;
}

.kpi-grid--quality {
  grid-template-columns: repeat(7, minmax(130px, 1fr));
}

.kpi-card {
  min-height: 92px;
  padding: 14px 14px 13px;
  border-left: 4px solid #8a96a8;
  box-sizing: border-box;
  transition: transform 0.18s ease, box-shadow 0.18s ease;
}

.kpi-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.08);
}

.kpi-card span {
  color: #596679;
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0;
}

.kpi-card strong {
  display: block;
  margin-top: 11px;
  color: #162235;
  font-size: 24px;
  line-height: 1;
}

.kpi-card small {
  display: block;
  margin-top: 8px;
}

.kpi-card--success {
  border-left-color: #52c41a;
}

.kpi-card--info {
  border-left-color: #1890ff;
}

.kpi-card--warning {
  border-left-color: #faad14;
}

.kpi-card--danger {
  border-left-color: #ff4d4f;
}

.kpi-card--neutral {
  border-left-color: #8a96a8;
}

.content-grid {
  display: grid;
  gap: 14px;
}

.content-grid--overview {
  grid-template-columns: minmax(0, 1.45fr) minmax(340px, 0.85fr);
}

.content-grid--collection {
  grid-template-columns: minmax(430px, 0.96fr) minmax(0, 1.04fr);
}

.panel {
  min-width: 0;
  padding: 14px;
  overflow: hidden;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
  padding-bottom: 10px;
  border-bottom: 1px solid #edf2f7;
}

.panel-head--wrap {
  align-items: flex-start;
  flex-direction: column;
}

.panel-head h2 {
  font-size: 15px;
  line-height: 1.25;
}

.source-list {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 12px;
}

.source-card {
  display: grid;
  gap: 10px;
  width: 100%;
  padding: 13px 14px 14px;
  border: 1px solid #e2e9f1;
  border-radius: 8px;
  background: #fbfcfe;
  color: inherit;
  text-align: left;
  cursor: pointer;
  transition: border-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease;
}

.source-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 10px 20px rgba(15, 23, 42, 0.06);
}

.source-card--active {
  border-color: #1890ff;
  box-shadow: 0 0 0 2px rgba(24, 144, 255, 0.12);
  background: #f7fbff;
}

.source-card > span:first-child {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.source-card b {
  color: #172235;
  font-size: 14px;
}

.source-card small,
.source-card dt {
  color: #6a7688;
}

.source-card dl,
.context-grid,
.health-grid,
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin: 0;
}

.source-card dt,
.context-item span,
.health-item span {
  font-size: 11px;
  font-weight: 700;
  color: #728196;
  text-transform: none;
}

.source-card dd,
.context-item strong,
.health-item strong {
  margin: 4px 0 0;
  color: #172235;
  font-size: 13px;
  word-break: break-word;
}

.context-grid {
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 10px;
}

.context-item,
.health-item {
  min-height: 72px;
  padding: 10px 11px;
  border: 1px solid #e3e9f1;
  border-radius: 6px;
  background: #fbfcfe;
  box-sizing: border-box;
}

.context-item {
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.pipeline-list {
  display: grid;
  gap: 10px;
}

.pipeline-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  min-height: 46px;
  padding: 9px 11px;
  border: 1px solid #e3e9f1;
  border-radius: 6px;
  background: #fbfcfe;
}

.event-timeline {
  padding: 2px 0 0 4px;
}

.event-timeline strong,
.event-timeline span {
  display: block;
}

.event-timeline span {
  margin-top: 3px;
  color: #6a7688;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.equipment-select {
  width: 220px;
}

.inline-filters {
  min-width: 460px;
}

.inline-filters :deep(.el-input),
.inline-filters :deep(.el-select),
.toolbar :deep(.el-input),
.toolbar :deep(.el-select) {
  width: 220px;
}

.trend-toolbar {
  display: grid;
  grid-template-columns: minmax(220px, 0.8fr) minmax(300px, 1.2fr);
  gap: 10px;
  margin-bottom: 10px;
}

.trend-chart {
  height: 340px;
}

.trend-empty {
  margin-top: -300px;
  pointer-events: none;
}

.history-toolbar :deep(.el-date-editor) {
  width: 360px;
}

.pagination-bar {
  display: flex;
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
  border-radius: 999px;
  font-size: 12px;
  font-weight: 800;
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

.status-badge--info {
  color: #1d5fa7;
  background: #e9f2ff;
}

.status-badge--warning {
  color: #8a5a00;
  background: #fff4dc;
}

.status-badge--danger {
  color: #b42318;
  background: #fff0ed;
}

.status-badge--neutral {
  color: #5f6b7a;
  background: #f0f3f7;
}

.detail-stack {
  display: grid;
  gap: 14px;
}

.detail-stack h3 {
  margin: 0;
  color: #152235;
  font-size: 18px;
}

.large-value {
  color: #152235;
  font-size: 30px;
  font-weight: 800;
  line-height: 1;
}

.large-value span {
  color: #6a7688;
  font-size: 14px;
}

:deep(.el-table th.el-table__cell) {
  background: #f5f7fa;
  color: #182433;
  font-weight: 800;
}

:deep(.el-table) {
  border: 1px solid #e3eaf2;
  border-radius: 8px;
  overflow: hidden;
}

:deep(.el-table__row:hover > td.el-table__cell) {
  background: #f2f8ff;
}

:deep(.el-button),
:deep(.el-input__wrapper),
:deep(.el-select__wrapper),
:deep(.el-input-number),
:deep(.el-textarea__inner) {
  border-radius: 6px;
}

:deep(.el-tabs__item) {
  height: 44px;
  line-height: 44px;
  font-weight: 700;
  color: #506074;
}

:deep(.el-tabs__item.is-active) {
  color: #1890ff;
}

:deep(.el-tabs__active-bar) {
  height: 3px;
  border-radius: 999px;
}

:deep(.el-tabs__nav-wrap::after) {
  height: 1px;
  background: #e4ebf2;
}

@media (max-width: 1440px) {
  .kpi-grid--quality {
    grid-template-columns: repeat(4, minmax(140px, 1fr));
  }

  .context-grid {
    grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  }
}

@media (max-width: 1100px) {
  .page-header,
  .panel-head {
    align-items: flex-start;
    flex-direction: column;
  }

  .kpi-grid,
  .kpi-grid--quality,
  .content-grid--overview,
  .content-grid--collection {
    grid-template-columns: 1fr;
  }

  .context-grid,
  .trend-toolbar,
  .form-grid {
    grid-template-columns: 1fr;
  }

  .inline-filters,
  .history-toolbar :deep(.el-date-editor) {
    min-width: 0;
    width: 100%;
  }
}
</style>
