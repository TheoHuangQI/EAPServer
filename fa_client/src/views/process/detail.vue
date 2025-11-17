<template>
  <div class="app-container flow-detail">
    <el-form ref="flowForm" :model="flowForm" label-width="120px">
      <el-row :gutter="20">
        <el-col :span="6">
          <el-form-item :label="流程名称" :rules="{required: true, message: `11111`, trigger: 'blur'}" prop="flowName">
            <el-input v-model="flowForm.flowName" clearable/>
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item :label="设备类型" prop="equipmentType" >
            <el-select v-model="flowForm.equipmentType" allow-create filterable clearable>
              <el-option v-for="(item,index) in eqpTypeList" :key="index" :value="item">{{ item }}</el-option>
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item :label="产品" prop="productId">
            <el-input v-model="flowForm.productId" clearable/>
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item :label="是否默认" prop="defaultFlow">
            <el-switch
              v-model="flowForm.defaultFlow"
              active-color="#13ce66"
              inactive-color="#ff4949"/>
          </el-form-item>
        </el-col>
        <el-col :span="24">
          <el-form-item label="FlowChart" >
            <flow-chart :flow-data="flowForm.flow" @updateFlowChartEvent="updateFlowChart"/>
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>
    <div slot="footer" class="dialog-footer" style="text-align: center;">
      <el-button :loading="loading" type="primary" @click="saveHandler('flowForm')">
      <svg-icon :icon-class="'save'" />&nbsp;保存</el-button>
      <el-button :loading="loading" type="primary" @click="cancelHandler">
      <svg-icon :icon-class="'cancel'" />&nbsp;取消</el-button>
    </div>
  </div>
</template>
<script>
import FlowChart from './components/flow-chart'
export default{
  components: { FlowChart },
  props: {
    detailData: {
      type: Object,
      default() {
        return {}
      }
    }
  },
  data() {
    return {
      loading: false,
      eqpTypeList: [],
      flowForm: {
        flowName: '',
        equipmentType: '',
        productId: '',
        defaultFlow: false,
        flow: {}
      }
    }
  },

  methods: {
    updateFlowChart($event) {
      $event.nodes.forEach(item => {
        item.type = item.text
      })
      Object.assign(this.flowForm.flow, $event)
    },
    saveHandler(formName) {
      if (this.flowForm.equipmentType === '' && this.flowForm.productId === '') {
        const confirmDel = '请输入产品名称'
        notice.error(confirmDel)
        return
      }
      this.$refs[formName].validate((valid) => {
        if (valid) {
          const { nodes = [], edges = [] } = this.flowForm.flow || {}
          notice.confirm('设定产品名称', () => {
            if (nodes.length < 3) {
              notice.error('流程错误')
              return false
            }
            if (edges.length < nodes.length - 1) {
              notice.error('22222')
              return false
            }
            if (this.flowForm.id > 0) {
            
            } else {
            }
          })
        }
      })
    },
    cancelHandler() {
      this.$emit('cancelEvent')
    }
  }
}
</script>
<style lang="scss" scoped>
  .flow-detail {

    .el-input,
    .el-select {
      width: 100%;
    }
  }
</style>
