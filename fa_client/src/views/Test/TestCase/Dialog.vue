<template>
  <el-dialog v-model:visible="dialogVisible" :title="isEdit ? '编辑测试用例' : '新增测试用例'" width="600px" custom-class="Test-Item-dialog"
    @close="handleClose">
    <el-form ref="testCase" :model="formData">
      <!-- Message Name -->
      <el-form-item label="测试用例名称" required>
        <el-input :disabled="this.isEdit" v-model="formData.testCaseName" placeholder="请输入测试用例名称"></el-input>
      </el-form-item>


      <el-form-item label="选择设备" required>
        <el-select v-model="formData.eqpId" placeholder="请选择测试设备" class="w-full">
          <el-option v-for="option in eqps" :key="option.eqpId" :label="option.eqpId" :value="option.eqpId"></el-option>
        </el-select>
      </el-form-item>


      <el-form-item label="选择模版" required>
        <el-select v-model="formData.tempName" placeholder="请选择测试模版" class="w-full">
          <el-option v-for="option in templates" :key="option.tempName" :label="option.tempName" :value="option.tempName"></el-option>
        </el-select>
      </el-form-item>
    </el-form>
    <template v-slot:footer>
<div  class="dialog-footer">
      <el-button @click="handleClose">取消</el-button>
      <el-button type="primary" @click="handleConfirm">确定</el-button>
    </div>
</template>
  </el-dialog>
</template>

<script>
import { GetList } from "@/api/equipment"
import { GetAllTemplate } from "@/api/Test"
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
      dialogVisible: false,
      isLoading: false,
      eqps:[],
      templates:[],
      formData: { ...this.form },
    };
  },
  created () {
    this.getEqpList()
    this.GetAllTemplate()
  },
  methods: {
    getEqpList(){
      GetList('').then(res=>{
            this.eqps = res.data.list
          })
    },
    GetAllTemplate(){
      GetAllTemplate().then(res=>{
            this.templates = res.data.list
          })
    },
    handleClose() {
      this.dialogVisible = false;
      console.log('Modal closed');
    },
    handleConfirm() {
      this.$refs.testCase.validate(valid => {
        if (valid) {
          this.formData.tempId=this.templates.find(a=>a.tempName==this.formData.tempName).id
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

<style lang="scss">
.Test-Item-dialog {
  .el-dialog__header {
    background: linear-gradient(to right, #1a56db, #3b82f6);
    padding: 16px 24px;
    border-radius: 3px 3px 0 0;
    height: auto;
  }

  .el-dialog__title {
    color: white !important;
    font-size: 16px;
    text-align: left;
  }

  .el-dialog__headerbtn {
    top: 16px;

    .el-dialog__close {
      color: white;
    }
  }

  .el-form {
    flex: 1;
    overflow-y: auto;
    padding: 0px 24px;
  }

  .w-full {
    width: 100%;
  }
  /* 确保选项组与标签垂直对齐 */
  .el-form-item__content {
    height: 60px;
  }

  .xml-status {
    display: flex;
    align-items: center;
    gap: 4px;
  }
}</style>