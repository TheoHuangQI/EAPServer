<template>
  <div id="editor">
    <toolbar @save="saveData"/>
    <div :style="{height:'42px'}" />
    <div class="bottom-container">
      <div  id="itempannel">
        <img :src="starten_icon" :data-label="起点" draggable="false" data-type="node" data-shape="flow-circle" data-size="72*72" data-color="green" data-text="start" class="getItem">
        <img :src="normalen_icon" :data-label="常规节点" draggable="false" data-type="node" data-shape="flow-rect" data-size="80*48" data-color="#1890FF" data-text="normal" class="getItem">
        <img :src="parallelen_icon" :data-label="并行开关" draggable="false" data-type="node" data-shape="flow-rhombus" data-size="80*72" data-color="yellow" data-text="parallel" class="getItem">
        <img :src="serialen_icon" :data-label="排他开关" draggable="false" data-type="node" data-shape="flow-rhombus" data-size="80*72" data-color="#13C2C2" data-text="exclusive" class="getItem">
        <img :src="enden_icon" :data-label="终点" draggable="false" data-type="node" data-shape="flow-circle" data-size="72*72" data-color="red" data-text="end" class="getItem">
      </div>
      <div id="detailpannel">
        <div id="node_detailpannel" data-status="node-selected" class="pannel">
          <div class="pannel-title">节点</div>
          <div class="block-container">
            <el-form label-width="90px" inline-form>
              <el-form-item v-show="false" label="ID">
                <el-input v-model="selectedModel.id" :disabled="true" size="mini"/>
              </el-form-item>
              <el-form-item v-show="false" label="X">
                <el-input-number v-model="selectedModel.x" :min="0" :precision="0" :disabled="true" size="mini"/>
              </el-form-item>
              <el-form-item v-show="false" label="Y">
                <el-input-number v-model="selectedModel.y" :min="0" :precision="0" :disabled="true" size="mini"/>
              </el-form-item>
              <el-form-item :label="节点名称">
                <el-input v-model="selectedModel.label" size="mini"/>
              </el-form-item>
              <el-form-item v-show="selectedModel.text==='normal'" :label="审批类型">
                <el-select v-model="selectedModel.signType" :placeholder="请选择">
                  <el-option
                    v-for="(item,index) in modeofreviewlist"
                    :key="index"
                    :label="getLanguage==='en'? item.labelen:item.label"
                    :value="item.value"/>
                </el-select>
              </el-form-item>
              <el-form-item v-show="selectedModel.text==='normal'" :label="审批人类型">
                <el-select v-model="selectedModel.auditRules" size="small" @change="selectedModel.approver = []">
                  <el-option :label="审批人" value="user"/>
                  <el-option :label="审批人1" value="group"/>
                </el-select>
              </el-form-item>

              <el-form-item v-show="selectedModel.text==='normal'" :label="审批人">
                <el-select v-model="selectedModel.approver" collapse-tags multiple size="small" filterable>
                  <el-option v-for="(item,index) in approverList" :key="index" :label="item.label" :value="item.value" />
                </el-select>
              </el-form-item>
            </el-form>
          </div>
        </div>
        <div id="edge_detailpannel" data-status="edge-selected" class="pannel">
          <div class="pannel-title">Side</div>
          <div class="block-container">
            <!-- {{ selectModel }} -->
          </div>
        </div>
        <div id="group_detailpannel" data-status="group-selected" class="pannel">
          <div class="pannel-title">Group</div>
          <div class="block-container">
            Test
          </div>
        </div>

        <div id="canvas_detailpannel" data-status="canvas-selected" class="pannel">
          <div class="pannel-title">Canvas</div>
          <div class="block-container">
            <el-checkbox @change="toggleGrid">单选</el-checkbox>
          </div>
        </div>
        <div id="multi_detailpannel" data-status="multi-selected" class="pannel">
          <div class="pannel-title">多选</div>
          <div class="block-container">
            Test
          </div>
        </div>
      </div>
      <navigator :cur-zoom="curZoom" :min-zoom="minZoom" :max-zoom="maxZoom" @change-zoom="changeZoom" />
      <page/>
    </div>
  </div>
</template>

<script>
import Navigator from './navigator'
import Toolbar from './toolbar'
import Editor from './editor'
import Page from './page'
import start_icon from './icons/start.svg'
import parallel_icon from './icons/parallel.svg'
import serial_icon from './icons/serial.svg'
import normal_icon from './icons/normal.svg'
import end_icon from './icons/end.svg'
import starten_icon from './icons/starten.svg'
import parallelen_icon from './icons/parallelen.svg'
import serialen_icon from './icons/serialen.svg'
import normalen_icon from './icons/normalen.svg'
import enden_icon from './icons/enden.svg'
import Axios from 'axios'
export default {
  components: {
    Navigator,
    Toolbar,
    Page
  },
  extends: Editor,
  props: {
    flowData: {
      type: Object,
      default() {
        return {
          nodes: [],
          edges: []
        }
      }
    }
  },
  data() {
    return {
      modeofreviewlist: [{ label: '串行', labelen: 'Exclusive', value: 'exclusive' }, { label: '并行', labelen: 'Parallel', value: 'parallel' }],
      start_icon: start_icon,
      parallel_icon: parallel_icon,
      serial_icon: serial_icon,
      normal_icon: normal_icon,
      end_icon: end_icon,
      starten_icon: starten_icon,
      parallelen_icon: parallelen_icon,
      serialen_icon: serialen_icon,
      normalen_icon: normalen_icon,
      enden_icon: enden_icon,
      approverList: [],
      selectModel: {}
    }
  },
  computed: {
    getLanguage() {
      return this.$store.getters.language
    } },
  watch: {
    'selectedModel.auditRules'(auditRules) {
      if (auditRules === 'user') {
        this.approverList = [...this.userList]
      } else {
        this.approverList = [...this.groupList]
      }
    },
    'flowData'() {
      this.page.read(this.flowData)
    }
  },
  created() {
    
  },
  mounted() {
    this.$nextTick().then(() => {
      this.page.changeAddEdgeModel({
        shape: 'flow-smooth'
      })
    })
  },
  methods: {
    saveData() {
      const { nodes = [], edges = [] } = this.page.save() || {}
      const startNodeList = nodes.filter(({ text }) => text === 'start')
      const normalNodeList = nodes.filter(({ text }) => text === 'normal')
      const endNodeList = nodes.filter(({ text }) => text === 'end')
      const self_join_count = edges.filter(({ source, target }) => source === target).length
      if (startNodeList.length === 0) {
        notice.error('缺少起始节点！')
        return false
      }
      if (normalNodeList.length === 0) {
        notice.error('缺少常规流程节点！')
        return false
      }
      if (endNodeList.length === 0) {
        notice.error('缺少终止节点！')
        return false
      }
      if (edges.length === 0) {
        notice.error('节点之间缺少关联关系！')
        return false
      }
      if (self_join_count > 0) {
        notice.error('节点不可以进行自关联')
        return false
      }
      if (edges.some(({ target }) => { return startNodeList[0].id === target })) {
        notice.error(`ID为[${startNodeList[0].id}]的节点错误的指向了起始节点！`)
        return false
      }
      if (edges.some(({ source }) => { return endNodeList[0].id === source })) {
        notice.error(`终止节点错误的指向了ID为[${startNodeList[0].id}]的节点！`)
        return false
      }
      if (edges.length < nodes.length - 1) {
        notice.error(`有流程节点未进行正确关联！`)
        return false
      }
      if (normalNodeList.filter(({ id }) => {
        return edges.filter(({ source, target }) => {
          return [source, target].includes(id)
        }).length < 2
      }).length > 0) {
        notice.error(`有流程节点未进行正确关联！`)
        return false
      }
      for (const { id, auditRules, approver } of normalNodeList) {
        if (!auditRules) {
          notice.error(`请为ID为[${id}]的节点设置审核规则！`)
          return false
        }
        if (!Array.isArray(approver) || approver.length === 0) {
          notice.error(`请为ID为[${id}]的节点设置审核对象！`)
          return false
        }
      }
      this.$emit('updateFlowChartEvent', this.page.save())
    }
  }
}
</script>
<style>
  @import url('./style/flow-chart.css');
</style>
<style lang="scss" scoped>
  #editor {
    border: 1px solid #e3e3e3;
  }

  #itempannel {
    height: 100%;
    position: absolute;
    left: 0px;
    z-index: 2;
    background: #F7F9FB;
    width: 200px;
    padding-top: 8px;
    border-right: 1px solid #E6E9ED;
  }

  #itempannel img {
    width: 92px;
    height: 96px;
    padding: 4px;
    margin-left: 4px;
    border-radius: 2px;
    border: 1px solid rgba(0, 0, 0, 0);
  }

  #itempannel img:hover {
    cursor: move;
    border: 1px solid #ccc;
  }

  #detailpannel {
    height: 100%;
    position: absolute;
    right: 0px;
    z-index: 2;
    background: #F7F9FB;
    width: 300px;
    border-left: 1px solid #E6E9ED;
  }

  #detailpannel .pannel {
    display: none
  }

  #detailpannel .block-containe {
    padding-top: 20px;
  }

  #detailpannel .input {
    margin-left: 16px;
  }

  #detailpannel .name-input {
    width: 120px;
  }

  #detailpannel .width-input {
    width: 52px;
  }

  #detailpannel .height-input {
    width: 52px;
  }

  #detailpannel .block-container {
    padding: 16px 8px;

    .el-input,
    .el-input-number,
    .el-select {
      width: 100%;
    }
  }

  .bottom-container {
    position: relative;
  }

  .pannel-title {
    height: 32px;
    border-top: 1px solid #DCE3E8;
    border-bottom: 1px solid #DCE3E8;
    background: #EBEEF2;
    color: #000;
    line-height: 28px;
    padding-left: 12px;
  }

  .zoom-dropdown-btn {
    color: rgba(0, 0, 0, 0.45);
    margin-left: 4px;
    line-height: 24px;
    font-weight: 100;
    text-decoration: none;
  }

  .color-picker {
    vertical-align: middle;
    margin-left: 16px;
  }

  .p {
    margin-bottom: 12px;
  }
</style>