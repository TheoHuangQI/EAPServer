<template>
  <div class="app-container">
    <!-- 操作栏 -->
    <el-card class="filter-card" shadow="never">
      <el-row :gutter="20">
        <el-col :xs="4" :sm="4" :md="4" :lg="4">
          <el-input v-model="pageInfo.eqpId" placeholder="设备ID" clearable @keyup.enter.native="handleSearch">
          </el-input>
        </el-col>
        <el-col :xs="4" :sm="4" :md="4" :lg="4">
          <el-input v-model="pageInfo.vid" placeholder="VID" clearable @keyup.enter.native="handleSearch">
          </el-input>
        </el-col>
        <el-col :xs="8" :sm="8" :md="8" :lg="8" class="operation-col">
          <el-button-group>
            <el-button type="primary" @click="handleSearch">
              <i class="el-icon-search" /> 查询
            </el-button>
            <el-button type="success" @click="handleAdd">
              <i class="el-icon-plus" /> 新增
            </el-button>
            <el-button type="danger" :disabled="selectedRows.length === 0" @click="handleBatchDelete">
              <i class="el-icon-delete" /> 批量删除
            </el-button>
          </el-button-group>
        </el-col>
      </el-row>
    </el-card>

    <!-- 数据表格 -->
    <el-card class="table-card" shadow="never">
      <el-table :data="tableData.slice((pageInfo.pageNo - 1) * pageInfo.pageSize, pageInfo.pageNo * pageInfo.pageSize)" style="width: 100%" stripe
        @selection-change="handleSelectionChange">
        <el-table-column type="selection" width="55" align="center" reserve-selection/>
        <el-table-column :index="indexHandler" label="序号" type="index" width="80" align="center" />
        <el-table-column prop="vid" label="VID" align="center" />
        <el-table-column prop="vidType" label="VID TYPE" align="center" >
          <template #default="scope">
            <span v-if="scope.row.vidType === 0">SVID</span>
            <span v-if="scope.row.vidType === 1">DVID</span>
            <span v-if="scope.row.vidType === 2">ECID</span>
          </template>
        </el-table-column>
        <el-table-column prop="eqpId" label="设备号" align="center" />
        <el-table-column prop="interval" label="采集间隔" align="center" />
        <el-table-column prop="count" label="采集次数" align="center" />
        <el-table-column prop="triggerType" label="触发方式" align="center">
          <template #default="scope">
            <span v-if="scope.row.triggerType === 0">EQP</span>
            <span v-if="scope.row.triggerType === 1">EVENT</span>
          </template>
        </el-table-column>
        <el-table-column prop="startEvent" label="开始事件" align="center" />
        <el-table-column prop="endEvent" label="结束事件" align="center" />
        <el-table-column prop="compareType" label="比对方式" align="center">
          <template #default="scope">
            <span v-if="scope.row.compareType === 0">值比对</span>
            <span v-if="scope.row.compareType === 1">范围比对</span>
          </template>
        </el-table-column>
        <el-table-column prop="compareValue" label="比对值" align="center" />
        <el-table-column prop="compareMax" label="最大值" align="center" />
        <el-table-column prop="compareMin" label="最小值" align="center" />
        <el-table-column prop="allowNumber" label="允许次数" align="center" />
        <el-table-column prop="action" label="执行动作" align="center" />
        <el-table-column label="操作" width="150" align="center">
          <template slot-scope="scope">
            <el-tooltip content="编辑" placement="top">
              <el-button type="primary" icon="el-icon-edit" circle size="mini" @click="handleEdit(scope.row)" />
            </el-tooltip>
            <el-tooltip content="删除" placement="top">
              <el-button type="danger" icon="el-icon-delete" circle size="mini" @click="handleDelete(scope.row)" />
            </el-tooltip>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
      <el-pagination class="pagination-wrapper" :current-page="pageInfo.pageNo" :page-sizes="[5, 10, 20]"
        :page-size="pageInfo.pageSize" :total="total" layout="total, sizes, prev, pager, next, jumper"
        @size-change="handleSizeChange" @current-change="handlePageChange" />
    </el-card>
  </div>
</template>
  
<script>
import { GetList, Add, Edit, Delete} from "@/api/dataRule"

export default {
  data() {
    return {
      loading: false,
      selectedRows: [],
      showModal: false,
      isEdit: false,
      tableData: [
      ],
      formData: this.getDefaultForm(),
      total: 0,
      pageInfo: {
        eqpId: '',
        vid: '',
        pageNo: 1,
        pageSize: 10,
      },
    }
  },
  created() {
    this.fetchData()
  },
  methods: {
    getDefaultForm() {
      return {
        vidType: '',
        vid: '',
        interval:null,
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
        action:''
      }
    },
    indexHandler(index) {
      return (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize + index + 1
    },
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
    handleSearch() {
      this.fetchData()
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
      this.$confirm('确定删除设备吗？', '警告', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Delete([row.id]).then(res => {
          if (res.return == "OK") {
            this.$message.success('删除成功')
            this.fetchData()
          }
          else {
            this.$message.error(res.message)
          }
        });
      }).catch(() => { })
    },
    handleBatchDelete() {
      if (this.selectedRows.length === 0) {
        this.$message.warning('请至少选择一条记录')
        return
      }
      this.$confirm(`确定删除选中的 ${this.selectedRows.length} 条记录吗？`, '警告', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Delete(this.selectedRows).then(res => {
          if (res.return == "OK") {
            this.$message.success('删除成功')
            this.fetchData()
          }
          else {
            this.$message.error(res.message)
          }
        });
      }).catch(() => { })
    },
    handleSelectionChange(selection) {
      this.selectedRows = selection.map(item => item.id)
    },
    handleSizeChange(val) {
      this.pageSize = val
      this.currentPage = 1
    },
    handlePageChange(val) {
      this.currentPage = val
    },
    getActionStr(action){
      return action==0?"StopEqp":"HoldLot"
    },
    handleDialogSubmit(formData) {
      this.loading = true
      try {
        if (this.isEdit) {
          formData.action='';
          formData.actions.forEach(element => {
            formData.action+=this.getActionStr(element)+';'
          });
          formData.actions=[];
          const res = Edit(formData).then(res => {
            if (res.return == "OK") {
              this.$message.success('修改成功')
              this.fetchData()
            }
            else {
              this.$message.error(res.message)
            }
          })
        }
        else {
          formData.actions.forEach(element => {
            formData.action+=this.getActionStr(element)+';'
          });
          formData.actions=[];
          const res = Add(formData).then(res => {
            if (res.return == "OK") {
              this.$message.success('添加成功')
              this.fetchData()
            }
            else {
              this.$message.error(res.message)
            }
          })
        }

      } finally {
        this.loading = false
      }
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
        text-align: left;
        margin-top: 10px;
      }
    }
  }

  .table-card {
    border-radius: 8px;
    border: 1px solid #ebeef5;

    ::v-deep .el-table {
      th {
        background-color: #f5f7fa;
      }
    }

    .pagination-wrapper {
      margin-top: 20px;
      text-align: right;
    }
  }

  .el-button-group {
    .el-button {
      &+.el-button {
        margin-left: 8px;
      }
    }
  }
}

.el-dialog {
  border-radius: 8px;

  .el-form-item {
    margin-bottom: 22px;
  }
}
</style>