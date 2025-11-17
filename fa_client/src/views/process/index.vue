<template>
  <div class="app-container">
    <div class="filter-container">
      <el-row :gutter="10">
        <el-col :sm="4">
          <el-input :placeholder="产品ID" v-model="pageInfo.productId" class="filter-item" clearable/>
        </el-col>
        <el-col :sm="4">
          <el-input :placeholder="流程名称" v-model="pageInfo.flowName" class="filter-item" clearable/>
        </el-col>
        <el-col :sm="4">
          <el-select :placeholder="请选择设备类型" v-model="pageInfo.equipmentType" filterable clearable>
            <el-option v-for="(item,index) in eqpTypeList" :key="index" :value="item">{{ item }}</el-option>
          </el-select>
        </el-col>
        <el-col :sm="4">
          <el-date-picker
            :placeholder="创建时间"
            v-model="pageInfo.createTime"
            format="yyyy 年 MM 月 dd 日"
            value-format="yyyy-MM-dd"
            type="date"/>
        </el-col>
        <el-col :sm="8">
          <el-button  class="filter-item" type="primary" icon="el-icon-search" @click="searchHandler">
          搜索</el-button>
          <el-button v-authorize="'xinzeng'"  type="primary" icon="el-icon-plus" class="filter-item" @click="addHandler">
           增加</el-button>
        </el-col>
      </el-row>
    </div>
    <el-table
      v-loading="loading"
      v-mousewheelX="'el-table__body-wrapper'"
      :data="dataList"
      size="small"
      border
      fit
      highlight-current-row
      @sort-change="sortchangeHandler">
      <el-table-column :index="indexHandler" :label="t('column.serialNumber')" type="index" width="80" align="center" />
      <el-table-column :label="t('column.flowName')" :show-overflow-tooltip="true" min-width="150" prop="flowName" align="center"/>
      <el-table-column
        :label="t('column.eqpType')"
        min-width="150"
        prop="equipmentType"
        align="center" />
      <el-table-column :label="t('column.productid')" min-width="150" prop="productId" align="center" sortable="custom"/>
      <el-table-column :label="t('column.isdefault')" width="150" prop="defaultFlow" sortable="custom" align="center">
        <template slot-scope="curr">
          <span v-if="curr.row.defaultFlow===1" style="font-size:20px;color:green;">
            <svg-icon :icon-class="'ok'" />
          </span>
          <span v-else style="font-size:20px;color:red;">
            <svg-icon :icon-class="'disable'" />
          </span>
        </template>
      </el-table-column>
      <el-table-column :label="t('column.createtime')" width="160" sortable="custom" prop="createTime" align="center" />
      <el-table-column :label="t('column.lastOperator')" width="160" prop="username" align="center" />
      <el-table-column :label="t('column.actions')" align="center" width="350" fixed="right">
        <template slot-scope="curr">
          <el-button v-authorize="'chakanliuchengtu'" type="success" size="mini" icon="el-icon-search" @click="lookHandler(curr.row)">{{ t('btn.flowchart') }}</el-button>
          <el-button v-authorize="'xiugai'" type="primary" size="mini" icon="el-icon-edit" @click="editHandler(curr.row)" >{{ t('btn.edit') }}</el-button>
          <el-button v-authorize="'shanchu'" type="danger" size="mini" icon="el-icon-delete" @click="removeHandler(curr.row,'deleted')">{{ t('btn.remove') }}</el-button>
        </template>
      </el-table-column>
    </el-table>
    <div class="pagination-container">
      <el-pagination
        :total="pageInfo.total"
        :page-size="pageInfo.pageSize"
        :page-sizes="pageInfo.pageSizeList"
        :current-page="pageInfo.pageNo"
        layout="total, sizes, prev, pager, next, jumper"
        @size-change="handleSizeChange"
        @current-change="handleCurrentChange" />
    </div>
    <el-dialog
      v-el-drag-dialog
      :visible.sync="showDetailForm"
      :fullscreen="true"
      :close-on-click-modal="false"
      :close-on-press-escape="false"
      top="5vh"
      width="1000px">
      <div slot="title">
        <svg-icon :icon-class="'proce'" />&nbsp;{{ t('dialog.addSigningProcess') }}
      </div>
      <flow-detail v-if="showDetailForm" :detail-data="flowDetail" @successEvent="successHandler" @cancelEvent="cancelHandler" />
    </el-dialog>
    <el-drawer
      :visible.sync="drawer"
      :modal="false"
      :close-on-press-escape="true"
      :modal-append-to-body="true"
      :title="t('column.flowchart')"
      direction="btt"
      size="40%">
      <flow-Chart
        v-if="drawer"
        :curr-flow="currFlow"
        @successEvent="successHandler"
        @cancelEvent="cancelHandler" />
    </el-drawer>

  </div>
</template>
<script>
import flowDetail from './detail'
import FlowChart from './goFlowChart'
export default {
  name: 'SigningList',
  components: { flowDetail,FlowChart },

  data() {
    return {
      currFlow: {},
      drawer: false,
      loading: false,
      showDetailForm: false,
      search: '',
      flowDetail: {},
      eqpTypeList: [],
      dataList: [],
      pageInfo: {
        total: 0,
        pageSize: 10,
        pageSizeList: [10, 20, 50, 100],
        pageNo: 1,
        sort: 'createTime',
        order: 'descending', // 'ascending||descending',
        productId: '',
        flowName: '',
        equipmentType: '',
        createTime: ''
      },
      flowList: []
    }
  },
 
  methods: {
    sortchangeHandler({ prop, order }) {
      switch (prop) {
        case 'flowName':
          prop = 'flowName'
          break
        case 'productId':
          prop = 'productId'
          break
        case 'defaultFlow':
          prop = 'defaultFlow'
          break
        case 'createTime':
          prop = 'createTime'
          break
      }
      this.pageInfo.sort = prop
      this.pageInfo.order = order
    },
  

    searchHandler() {
    },
    addHandler() {
      this.showDetailForm = true
      this.flowDetail = {}
    },
    editHandler(row) {
      // const jsObject = JSON.parse(jsonString);
      this.showDetailForm = true
      this.flowDetail = row
    },
    removeHandler(row) {
      notice.confirm(this.t('tip.confirmRemove'), () => {
       
      })
    },
    successHandler(title, res) {
      this.showDetailForm = false
      notice.success(this.t('tip.executionSucceed'), () => {
      })
    },
    cancelHandler() {
      this.showDetailForm = false
    },
    lookHandler(row) {
      this.currFlow = row
      this.drawer = true
    },
    handleSizeChange(val) {
      this.pageInfo.pageSize = val
    },
    handleCurrentChange(val) {
      this.pageInfo.pageNo = val
    },
    indexHandler(index) {
      return (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize + index + 1
    }
  }
}
</script>
<style lang="scss" scoped>
  .el-select,
  .el-input-number,
  .el-date-editor {
    width: 100%;
  }
   .box-card {
    padding: 0;
    margin: 0;
  }
</style>
