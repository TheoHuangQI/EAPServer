<template>
  <el-dialog :title="isEdit ? '编辑设备信息' : '添加设备'" :visible.sync="dialogVisible" width="600px" :close-on-click-modal="false"
    @close="handleClose">
    <el-form ref="form" :model="formData" :rules="rules" label-width="100px">
      <!-- 设备号 -->
      <el-form-item label="设备号" prop="eqpId">
        <el-input :disabled="this.isEdit" v-model="formData.eqpId" placeholder="请输入设备号" clearable style="width: 100%" />
      </el-form-item>

      <!-- 设备类型 -->
      <el-form-item label="设备类型" prop="eqpType">
        <el-input  v-model="formData.eqpType" placeholder="请输入设备类型" clearable
          style="width: 100%" />
      </el-form-item>

      <!-- 载具选择 -->
      <el-form-item label="启用" prop="enable">
        <el-select v-model="formData.enable" placeholder="是否启用设备" clearable style="width: 100%">
          <el-option v-for="option in enableOption" :key="option.index" :label="option.value" :value="option.index" />
        </el-select>
      </el-form-item>
      <!-- 操作按钮 -->
      <el-form-item class="dialog-footer">
        <el-button @click="handleClose">取消</el-button>
        <el-button type="primary" @click="handleConfirm">确 定</el-button>
      </el-form-item>
    </el-form>
  </el-dialog>
</template>

<script>
import { Alert } from 'element-ui'
export default {
  props: {
    visible: {
      type: Boolean,
      default: false
    },
    isEdit: {
      type: Boolean,
      default: false
    },
    form: {
      type: Object,
      default: () => ({})
    }
  },
  data() {
    const validateQty = (rule, value, callback) => {
      if (value === '') return callback(new Error('请输入数量'))
      if (typeof value !== 'number') return callback(new Error('必须为数字'))
      if (value < 0 || value > 25) {
        callback(new Error('数量必须在0-25之间'))
      } else {
        callback()
      }
    }
    return {
      dialogVisible: this.visible,
      formData: { ...this.form },
      enableOption: [{index:0,value:"Disable"},{index:1,value:"Enable"}],
      rules: {
        eqpId: [
          { required: true, message: '请输入批次号', trigger: 'blur' }
        ],
        eqpType: [
          { required: true, message: '请输入设备类型', trigger: 'blur' }
        ],
        enable: [
          { required: true, message: '请选择是否启用设备', trigger: 'blur' }
        ],
      }
    }
  },
  methods: {
    handleClose() {
      this.dialogVisible = false
    },
    handleConfirm() {
      this.$refs.form.validate(valid => {
        if (valid) {
          this.$emit('submit', { ...this.formData })
          this.dialogVisible = false
        }
      })
    }
  },
  watch: {
    visible(newVal) {
      this.dialogVisible = newVal
      if (newVal) {
        this.formData = { ...this.form }
      }
    },
    dialogVisible(newVal) {
      this.$emit('update:visible', newVal)
    },
  }
}
</script>
<style scoped>
.dialog-footer {
  margin-top: 20px;
  text-align: right;
}
</style>