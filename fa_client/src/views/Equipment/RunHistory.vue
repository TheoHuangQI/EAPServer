<template>
    <div class="app-container">
      <el-card class="filter-card" shadow="never">
      <!-- 修改后的栅格布局 -->
      <el-row :gutter="10">
        <!-- 搜索条件列 -->
        <el-col :xs="24" :sm="6" :md="4" :lg="4">
          <el-input v-model="pageInfo.eqpId" placeholder="设备ID" />
        </el-col>
        <el-col :xs="24" :sm="6" :md="4" :lg="4">
          <el-input v-model="pageInfo.eqpType" placeholder="设备类型" />
        </el-col>
        <!-- 操作按钮列 -->
        <el-col :xs="24" :sm="24" :md="12" :lg="12">
          <div class="action-group">
            <el-button type="primary" icon="el-icon-search"  @click="handleSearch">查询</el-button>
            <el-button type="primary" icon="el-icon-download" @click="handleExport">导出</el-button>
            <el-button type="danger" icon="el-icon-delete" :disabled="selectedRows.length === 0"   @click="handleBatchDelete">批量删除</el-button>
          </div>
        </el-col>
      </el-row>
      </el-card>
      <!-- 数据表格 -->
      <el-card class="table-card" shadow="never">
        <el-table v-loading="loading" :data="tableData.slice((pageInfo.pageNo-1)*pageInfo.pageSize,pageInfo.pageNo*pageInfo.pageSize)" style="width: 100%" stripe row-key="id"
          @selection-change="handleSelectionChange">
          <el-table-column type="selection" width="55" align="center" reserve-selection />
          <el-table-column label="序号" width="50" align="center">
            <template #default="scope">
              {{ (pageInfo.pageNo - 1) * pageInfo.pageSize + scope.$index + 1 }}
            </template>
          </el-table-column>
          <el-table-column prop="eqpId" label="设备号" width="100" align="center" />
          <el-table-column prop="eqpType" label="设备类型" width="150" align="center" />
          <el-table-column prop="portName" label="PORT" width="100" align="center" />
          <el-table-column prop="carrierId" label="CARRIER" width="100" align="center" />
          <el-table-column prop="lotId" label="LOT" width="200" align="center" />
          <el-table-column prop="state" label="状态" width="100" align="center">
            <template #default="scope">
              <el-tag :type="scope.row.state === 0 ? 'warning' : 'success'">
                {{ scope.row.state === 0 ? 'START' :scope.row.state === 1 ?'RUN':'END' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="startTime" label="开始时间" width="200"/>
          <el-table-column prop="endTime" label="结束时间" width="200"/>
          <el-table-column label="操作"  align="center">
            <template #default="scope">
              <el-tooltip content="删除" placement="top">
                <el-button type="danger" icon="el-icon-delete" circle size="small" @click="handleDelete(scope.row)" />
              </el-tooltip>
            </template>
          </el-table-column>
        </el-table>
  
        <!-- 分页 -->
        <el-pagination class="pagination-wrapper" :current-page="pageInfo.pageNo" :page-sizes="[10, 20, 50]"
          :page-size="pageInfo.pageSize" :total="total" layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange" @current-change="handlePageChange" />
      </el-card>
    </div>
  </template>
  
  <script>
  import { GetList, Delete } from "@/api/runHistory"
  
  export default {
    data() {
      return {
        loading: false,
        selectedRows: [],
        tableData: [],
        total: 0,
        pageInfo: {
          eqpId: '',
          eqpType: '',
          pageNo: 1,
          pageSize: 10,
        }
      }
    },
    created() {
      this.fetchData()
    },
    methods: {
      // 数据获取
      async fetchData() {
        this.loading = true
        try {
          const res = await GetList(this.pageInfo)
          this.tableData = res.data.list
          this.total = res.data.total
        } finally {
          this.loading = false
        }
      },
  
      // 搜索
      handleSearch() {
        this.pageInfo.pageNo = 1
        this.fetchData()
      },
  
      // 分页
      handleSizeChange(val) {
        this.pageInfo.pageSize = val
        this.pageInfo.pageNo = 1
        this.fetchData()
      },
      handlePageChange(val) {
        this.pageInfo.pageNo = val
        this.fetchData()
      },
  
      // 批量操作
      handleSelectionChange(selection) {
        this.selectedRows = selection.map(item => item.id)
      },
      handleBatchDelete() {
        this.$confirm(`确定删除选中的 ${this.selectedRows.length} 条记录吗？`, '警告', {
          confirmButtonClass: 'el-button--danger'
        }).then(async () => {
          Delete(this.selectedRows).then(res => {
            if (res.return == "OK") {
              this.$message.success('删除成功')
              this.fetchData()
            }
            else {
              this.$message.error(res.message)
            }
          });
        }).catch(() => null)
      },
      handleDelete(row) {
        this.$confirm('确定删除该记录吗？', '警告', {
          confirmButtonClass: 'el-button--danger'
        }).then(async () => {
          Delete([row.id]).then(res => {
            if (res.return == "OK") {
              this.$message.success('删除成功')
              this.fetchData()
            }
            else {
              this.$message.error(res.message)
            }
          });
        }).catch(() => null)
      },
      // 导出
      async handleExport() {
        this.$message.warning('导出功能暂未实现')
      }
    }
  }
  </script>
  
  <style lang="scss" scoped>
  .app-container {
    padding: 20px;
    background-color: #f5f7fa;
  
    .filter-card {
      :deep() .el-card__body {
        padding: 18px 20px;
      }
  
      .el-col {
        margin-bottom: 12px;
        
        // 最后一个列取消底部间距
        &:last-child {
          margin-bottom: 0;
        }
  
        // 中等屏幕以上布局
        @media (min-width: 992px) {
          margin-bottom: 0;
          display: flex;
          align-items: center;
          min-height: 40px;  // 与输入框高度保持一致
        }
      }
  
      .action-group {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
        
        // 中等屏幕以上布局
        @media (min-width: 992px) {
          justify-content: flex-end;
        }
  
        // 小屏幕布局
        @media (max-width: 991px) {
          .el-button {
            flex: 1 1 48%; // 两列布局
            min-width: 120px;
          }
        }
  
        // 超小屏幕布局
        @media (max-width: 480px) {
          .el-button {
            flex: 1 1 100%; // 单列布局
          }
        }
      }
  
      // 统一输入组件高度
      .el-input, .el-select {
        :deep(.el-input__inner) {
          height: 40px;
          line-height: 40px;
        }
      }
    }
  
    .table-card {
      :deep(.el-table) {
        background-color: #fafafa;
      }
  
      .el-tag {
        padding: 0 8px;
        height: 24px;
        line-height: 24px;
      }
  
      .pagination-wrapper {
        margin-top: 20px;
        display: flex;
        justify-content: flex-end;
      }
    }
  
    @media (max-width: 768px) {
      .el-col {
        margin-bottom: 10px;
  
        &:last-child {
          margin-bottom: 0;
        }
      }
    }
  }
  </style>
