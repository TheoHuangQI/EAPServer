<template>
  <el-dialog
    :title="isEdit ? '编辑载具信息' : '新增载具信息'"
    :visible.sync="dialogVisible"
    width="500px"
    :close-on-click-modal="false"
    @closed="handleClosed"
  >
    <el-form ref="formRef" :model="formData" label-width="100px">
      <el-form-item 
        label="载具编号" 
        prop="carrierId"
        :rules="[{ required: true, message: '请输入载具编号', trigger: 'blur' }]"
      >
        <el-input v-model="formData.carrierId" placeholder="请输入载具编号" />
      </el-form-item>
      <el-form-item
        label="尺寸"
        prop="size"
        :rules="[{ required: true, message: '请输入尺寸', trigger: 'blur' }]"
      >
        <el-input v-model="formData.size" placeholder="请输入尺寸" />
      </el-form-item>
      <el-form-item
        label="使用状态"
        prop="in_use"
        :rules="[{ required: true, message: '请选择使用状态', trigger: 'change' }]"
      >
        <el-select v-model="formData.in_use" placeholder="请选择状态" style="width: 100%">
          <el-option label="使用中" :value="1" />
          <el-option label="空闲" :value="0" />
        </el-select>
      </el-form-item>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="dialogVisible = false">取 消</el-button>
        <el-button type="primary" @click="handleConfirm">确 定</el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script>
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
    return {
      dialogVisible: this.visible,
      formData: { ...this.form }
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
    }
  },
  methods: {
    handleConfirm() {
      this.$refs.formRef.validate(valid => {
        if (valid) {
          this.$emit('submit', { ...this.formData })
          this.dialogVisible = false
        }
      })
    },
    handleClosed() {
      this.$refs.formRef.resetFields()
    }
  }
}
</script>