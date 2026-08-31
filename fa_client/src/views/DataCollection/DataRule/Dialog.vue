<template>
  <el-dialog
    v-model:visible="dialogVisible"
    title="VID 采集配置"
    width="600px"
    custom-class="vid-monitor-dialog"
    @close="handleClose"
  >
    <el-form
      ref="form"
      :model="formData"
      label-width="110px"
      label-position="left"
      class="demo-form-stacked"
    >
      <!-- 基本配置 -->
      <div class="form-section">
        <h3 class="section-title">基本配置</h3>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="VID 类型" prop="vidType" required>
              <el-select
                v-model="formData.vidType"
                placeholder="请选择 VID 类型"
              >
                <el-option
                  v-for="type in vidTypes"
                  :key="type.index"
                  :label="type.value"
                  :value="type.index"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="VID 值" prop="vid" required>
              <el-input
                v-model="formData.vid"
                placeholder="请输入 VID 值"
              />
            </el-form-item>
          </el-col>
        </el-row>

        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="采集频率" prop="interval" required>
              <el-input
                v-model="formData.interval"
                placeholder="采集频率"
                type="number"
              >
                <template v-slot:append>S</template>
              </el-input>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="采集次数" prop="count">
              <el-input
                v-model="formData.count"
                placeholder="0 表示持续采集"
                type="number"
              />
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item label="设备号" prop="eqpId" required>
          <el-select
            v-model="formData.eqpId"
            placeholder="请选择设备号"
          >
            <el-option
              v-for="eqp in eqps"
              :key="eqp.eqpId"
              :label="eqp.eqpId"
              :value="eqp.eqpId"
            />
          </el-select>
        </el-form-item>
      </div>

      <!-- 触发设置 -->
      <div class="form-section">
        <h3 class="section-title">触发设置</h3>
        <el-form-item label="触发方式" prop="triggerType" required>
          <el-radio-group v-model="formData.triggerType" class="radio-group">
            <el-radio :label="0">EQP</el-radio>
            <el-radio :label="1">EVENT</el-radio>
          </el-radio-group>
        </el-form-item>

        <template v-if="formData.triggerType === 1">
          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="开始事件" prop="startEvent" required>
                <el-input
                  v-model="formData.startEvent"
                  placeholder="开始采集事件"
                  type="number"
                />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="结束事件" prop="endEvent" required>
                <el-input
                  v-model="formData.startEvent"
                  placeholder="结束采集事件"
                  type="number"
                />
              </el-form-item>
            </el-col>
          </el-row>
        </template>
      </div>
      <!-- 比对配置 -->
      <div class="form-section">
        <h3 class="section-title">比对配置</h3>
        <el-form-item label="比对方式" prop="compareType" required>
          <el-radio-group v-model="formData.compareType" class="radio-group">
            <el-radio :label="0" :value="0">时值比对</el-radio>
            <el-radio :label="1" :value="1">范围比对</el-radio>
          </el-radio-group>
        </el-form-item>

        <template v-if="formData.compareType === 0">
          <el-form-item label="比对值" prop="compareValue" required>
            <el-input
              v-model="formData.compareValue"
              placeholder="请输入比对值"
              type="number"
            />
          </el-form-item>
        </template>

        <template v-if="formData.compareType === 1">
          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="最小值" prop="compareMin" required>
                <el-input
                  v-model="formData.compareMin"
                  placeholder="请输入最小值"
                  type="number"
                />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="最大值" prop="compareMax" required>
                <el-input
                  v-model="formData.compareMax"
                  placeholder="请输入最大值"
                  type="number"
                />
              </el-form-item>
            </el-col>
          </el-row>
        </template>

        <el-form-item label="限制次数" prop="limitTimes" required>
          <el-input
            v-model="formData.limitTimes"
            placeholder="请输入限制次数"
            type="number"
          />
        </el-form-item>
      </div>

      <!-- 执行动作 -->
      <div class="form-section">
        <h3 class="section-title">执行动作</h3>
        <el-form-item label="选择动作" prop="actions" required>
          <el-checkbox-group v-model="formData.actions" class="checkbox-group">
            <el-checkbox :label="0">StopEqp</el-checkbox>
            <el-checkbox :label="1">HoldLot</el-checkbox>
          </el-checkbox-group>
        </el-form-item>
      </div>
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
import { GetList} from "@/api/equipment"
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
      formData: { ...this.form },
      vidTypes: [{index:0,value:'DVID'}, {index:1,value:'SVID'}, {index:2,value:'ECID'}],
      formData: {
      },
      eqps:[]
    }
  },
  created () {
    this.getEqpList()
  },
  methods: {
    getEqpList(){
      GetList('').then(res=>{
            this.eqps = res.data.list
          })
    },
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

<style lang="scss">
.vid-monitor-dialog {
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

  .el-form-item {
    margin-bottom: 16px;
    width: 100%;
  }

  .section-title {
    font-size: 16px;
    color: #374151;
    margin: 0 0 16px 0;
    padding-bottom: 8px;
    border-bottom: 1px solid #e5e7eb;
  }

  .el-input__inner {
    height: 36px;
    line-height: 36px;
  }

  .el-select {
    width: 100%;
  }

  .el-radio, .el-checkbox {
    margin-right: 16px;
  }

  .dialog-footer {
    padding: 16px 10px;
    border-top: 1px solid #e5e7eb;
  }

  .input-with-unit .el-input-group__append {
    padding: 0 12px;
    background: white;
    border-left: 0;
  }

  /* 移除数字输入框箭头 */
  input[type=number]::-webkit-inner-spin-button,
  input[type=number]::-webkit-outer-spin-button {
    -webkit-appearance: none;
    margin: 0;
  }
  input[type=number] {
    -moz-appearance: textfield;
  }

  /* 确保所有内容靠左对齐 */
  .el-form-item__label {
    text-align: left;
    line-height: 36px;
    height: 36px;
  }

  .section-title {
    text-align: left;
  }

  .el-row {
    justify-content: flex-start;
  }

  .el-col {
    text-align: left;
  }

  /* 确保选项在同一行并靠左对齐 */
  .radio-group, .checkbox-group {
    display: flex;
    justify-content: flex-start;
    align-items: center;
    flex-wrap: wrap;
    width: 100%;
  }

  .el-radio, .el-checkbox {
    text-align: left;
    justify-content: flex-start;
    display: flex;
    align-items: center;
    margin-right: 16px;
    margin-bottom: 8px;
    white-space: nowrap;
  }

  .el-radio__label, .el-checkbox__label {
    text-align: left;
  }

  /* 确保选项组与标签垂直对齐 */
  .el-form-item__content {
    display: flex;
    align-items: center;
    height: 36px;
    line-height: 36px;
  }

  /* 确保 el-radio 的高度与标签一致 */
  .el-radio {
    height: 36px;
    line-height: 36px;
  }
}
</style>