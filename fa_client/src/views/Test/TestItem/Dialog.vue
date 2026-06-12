<template>
  <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑测试项' : '新增测试项'" width="600px"
    custom-class="Test-Item-dialog" @close="handleClose">
    <el-form ref="messageForm" :model="formData">
      <!-- Message Name -->
      <el-form-item label="Message Name" required>
        <el-input :disabled="this.isEdit" v-model="formData.messageName" placeholder="Enter message name"></el-input>
      </el-form-item>

      <!-- Description -->
      <el-form-item label="Description" required>
        <el-input v-model="formData.description" placeholder="Enter description"></el-input>
      </el-form-item>

      <!-- Stream & Function -->
      <el-row :gutter="16" class="form-row">
        <el-col :span="12">
          <el-form-item label="Stream" required>
            <el-input v-model="formData.stream" placeholder="Enter stream number"></el-input>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="Function" required>
            <el-input v-model="formData.function" placeholder="Enter function number"></el-input>
          </el-form-item>
        </el-col>
      </el-row>

      <!-- Direction -->
      <el-form-item label="Direction" required>
        <el-select v-model="formData.direction" placeholder="Select direction" class="w-full">
          <el-option v-for="option in directions" :key="option" :label="option" :value="option"></el-option>
        </el-select>
      </el-form-item>

      <!-- Wait & Auto Reply -->
      <el-row :gutter="16" class="form-row">
        <el-col :span="12">
          <el-form-item label="Wait Status" required>
            <el-switch v-model="wait" @change="handleWaitSwitch"></el-switch>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="Auto Reply" required>
            <el-switch v-model="autoReply" @change="handleReplySwitch"></el-switch>
          </el-form-item>
        </el-col>
      </el-row>

      <!-- Stream & Function -->
      <el-row :gutter="16" class="form-row" v-if="autoReply === true">
        <el-col :span="12">
          <el-form-item label="Reply Stream" required>
            <el-input v-model="formData.replyStream" placeholder="Enter Reply stream number"></el-input>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="Reply Function" required>
            <el-input v-model="formData.replyFunction" placeholder="Enter Reply function number"></el-input>
          </el-form-item>
        </el-col>
      </el-row>

      <!-- Data Item Content -->
      <el-form-item label="Data Item Content">
        <el-input type="textarea" v-model="formData.dataItemContent"  placeholder="Enter XML format content"
          @input="validateXML" :class="{ 'invalid-xml': !isValidXML && formData.dataItemContent }"></el-input>
      </el-form-item>

      <!-- Data Item Content -->
      <el-form-item label="Data Item Reply Content" v-if="autoReply === true">
        <el-input type="textarea" v-model="formData.replyDataItemContent"  placeholder="Enter XML format reply content"
          @input="validateXML" :class="{ 'invalid-xml': !isValidXML && formData.replyDataItemContent }"></el-input>
      </el-form-item>
    </el-form>
    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">取消</el-button>
        <el-button type="primary" @click="handleConfirm">确定</el-button>
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
      dialogVisible: false,
      isLoading: false,
      isValidXML: true,
      wait: false,
      autoReply: false,
      directions: ['H->E', 'E->H', 'H<->E'],
      formData: { ...this.form },
    };
  },
  computed: {
    characterCount() {
      return this.formData.dataItemContent.length;
    },
  },
  methods: {
    handleWaitSwitch(value) {
      this.formData.wait = value ? 1 : 0; // 将true或false转换为1或0
    },

    handleReplySwitch(value) {
      this.formData.autoReply = value ? 1 : 0; // 将true或false转换为1或0
    },

    validateXML() {
      if (!this.formData.dataItemContent) {
        this.isValidXML = true;
        return;
      }
      try {
        // 使用 DOMParser 验证 XML
        new DOMParser().parseFromString(this.formData.dataItemContent, 'text/xml');
        this.isValidXML = true;
      } catch (error) {
        this.isValidXML = false;
      }
    },
    handleClose() {
      this.dialogVisible = false;
      console.log('Modal closed');
    },
    handleConfirm() {
      this.$refs.messageForm.validate(valid => {
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
        this.wait = this.formData.wait ? true : false;
        this.autoReply = this.formData.autoReply ? true : false;
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

  .xml-status {
    display: flex;
    align-items: center;
    gap: 4px;
  }
}
</style>