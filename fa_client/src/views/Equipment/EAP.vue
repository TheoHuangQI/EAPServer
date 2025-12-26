<template>
  <div class="container">
    <el-row class="main-wrapper" :gutter="10">
      <!-- 左侧列表 -->
      <el-col :span="3">
        <div class="left-panel">
          <div class="search-container">
            <el-input v-model="searchQuery" placeholder="筛选" clearable>
              <i slot="prefix" class="el-input__icon el-icon-search"></i>
            </el-input>
          </div>
          <div class="list-container">
            <div v-for="(item, index) in filteredList" :key="index" class="list-item"
              :class="{ 'active-item': selectedItem === item }" @click="selectItem(item)">
              <div class="item-content">
                <el-row>
                  <el-col :span="12">
                    <div class="item-title">{{ item.eqpId }}</div>
                  </el-col>
                  <el-col :span="12">
                    <div class="item-desc">
                      {{ item.eqpStatus == 0 ? 'Disconnected' : 'Connected' }}
                    </div>
                  </el-col>
                </el-row>
                <div class="item-desc">
                      {{ getControlState(item.controlState )}}
                    </div>
              </div>
            </div>
          </div>
        </div>
      </el-col>
      <!-- 右侧内容 -->
      <el-col :span="21">
        <div class="right-panel">
          <!-- LP 信息卡片区域 -->
          <div class="lp-section">
            <el-row :gutter="5">
              <el-col :span="6" v-for="(lp, index) in selectedItem.lpList" :key="index">
                <el-card class="lp-card">
                  <div class="lp-header">
                    <span class="lp-name">{{ lp.portName }}</span>
                    <el-tag :type="'success'">
                      {{ getCarrierId(lp.carrierId) }}
                    </el-tag>
                  </div>
                  <div class="lp-content">
                    <!-- 25 槽垂直排列 -->
                    <!-- 外层容器 -->
                    <div class="slot-container">
                      <!-- 左侧数字列 -->
                      <div class="slot-numbers">
                        <div v-for="number in 25" :key="number" class="slot-number">
                          {{ number }}
                        </div>
                      </div>

                      <!-- 右侧槽位容器 -->
                      <div class="slot-column">
                        <div v-for="(slot, index) in lp.slots" :key="index" class="slot">
                          <div class="slot-line" :style="{ backgroundColor: getSlotColor(slot.filled) }"></div>
                        </div>
                      </div>
                    </div>
                    <!-- 右侧三行信息 -->
                    <div class="lp-info">
                      <el-tag class="info-tag"> {{ getLotId(lp.lotId) }}</el-tag>
                      <el-tag class="info-tag"> {{ getTransferState(lp.transferState) }}</el-tag>
                      <el-tag class="info-tag"> {{ getAceessMode(lp.accessMode )}}</el-tag>
                    </div>
                  </div>
                </el-card>
              </el-col>
            </el-row>
          </div>
          <!-- 日志滚动区域（保持不变） -->
          <div class="log-section">
            <el-card class="log-box">
              <div class="log-header">实时日志</div>
              <div class="log-container">
                <div v-for="(log, index) in selectedItem.logs" :key="index" class="log-item" :class="log.type">
                  <span class="log-time">[{{ log.time }}]</span>
                  <span class="log-content">{{ log.message }}</span>
                </div>
              </div>
            </el-card>
          </div>
        </div>
      </el-col>
    </el-row>



  </div>
</template>
  
<script>
import * as signalR from '@microsoft/signalr';
import { GetEqpInfo } from "@/api/equipment"
export default {
  data() {
    return {
      loading: false,
      searchQuery: '',
      eqpInfos: [],
      selectedItem: null
    };
  },
  methods: {
    getLotId(lotId){
      if(lotId==null)
      if(lotId==null || toString(lotId).trim().length==0){
        return "UnKnow"
      }
      else{
        return lotId
      }
    },
    getCarrierId(carrierId){
      if(carrierId==null || toString(carrierId).trim().length==0){
        return "UnKnow"
      }
      return carrierId
    },
    getAceessMode(accessMode){
      const accessMap = {
        0: 'Manual',
        1: 'Auto',
      };
      return accessMap[accessMode] || 'Manual'
    },
    getTransferState(transferState){
      const transferMap = {
        0: 'OutOfService',
        1: 'Blocked',
        2: 'ReadyToLoad',
        3: 'ReadyToUnload',
        4: 'InService',
        5: 'TransferReady'
      };
      return transferMap[transferState] || 'OutOfService'
    },
    getControlState(controlState){
      const controlMap = {
        0: 'UnKnow',
        1: 'EqpOffline',
        2: 'Attempt',
        3: 'HostOffline',
        4: 'Local',
        5: 'Remote'
      };
      return controlMap[controlState] || 'UnKnow'
    },
    getSlotColor(filled) {
      const colorMap = {
        0: '#dcdfe9',
        1: '#dcdfe9',
        3: '#4F94CD',
        4: '#68228B',
        5: '#67c23a'
      };
      return colorMap[filled] || '#dcdfe9'; // 默认透明
    },
    selectItem(item) {
      this.selectedItem = item;
    },
    initSignalR() {
      // 创建 SignalR 连接
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl("https://localhost:7255/logHub", {
          skipNegotiation: true, // 跳过协商（如果使用 WebSocket）
          transport: signalR.HttpTransportType.WebSockets, // 强制使用 WebSocket
          withCredentials: true // 允许凭据
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();
      this.connection.start()
        .then(() => console.log("SignalR Connected"))
        .catch(err => console.error("SignalR Connection Error: ", err));

      // 监听后端推送的日志消息
      this.connection.on("ReceiveLog", (log) => {
        var eqpId = log.eqpId;
        var eqpInfo = this.eqpInfos.find(a => a.eqpId == eqpId);
        if (eqpInfo != null) {
          if (log.type == "Log") {
            eqpInfo.logs.push({
              time: log.time,
              type: log.msgType,
              message: log.message
            });
            // 保持最多 200 条日志
            if (eqpInfo.logs.length > 200) {
              eqpInfo.logs.shift();
            }
            if (this.selectedItem.eqpId == eqpId) {
              // 自动滚动到底部
              this.$nextTick(() => {
                const container = this.$refs.logContainer;
                container.scrollTop = container.scrollHeight;
              });
            }
          }
          else if (log.type == "Map") {
            var maps = log.message.split(";")
            var lpinfo = eqpInfo.lpList.find(m => m.portId == log.portId)
            if (lpinfo != null) {
              if (maps.length == lpinfo.slots.length) {
                lpinfo.slots.forEach((slot, index) => {
                  slot.filled = maps[index]; // 或者任何其他的赋值逻辑
                });
              }
              else {
                maps.forEach((slot) => {
                  lpinfo.slots[Number(slot) - 1].filled = 4;
                });
              }
            }
          }
          else if (log.type == "SubStrate") {
            var lpinfo = eqpInfo.lpList.find(m => m.portId == log.portId)
            lpinfo.slots[Number(log.message) - 1].filled = 5;
          }
          else if(log.type=="ControlState"){
            eqpInfo.controlState=log.message;
          }
          else if(log.type=="AccessMode"){
            var lpinfo = eqpInfo.lpList.find(m => m.portId == log.portId)
            lpinfo.accessMode=log.message;
          }
          else if(log.type=="TransferState"){
            var lpinfo = eqpInfo.lpList.find(m => m.portId == log.portId)
            lpinfo.transferState=log.message;
          }
          else if(log.type=="EapState"){
            eqpInfo.eapStatus=log.message;
          }
          else if(log.type=="EqpState"){
            eqpInfo.eqpStatus=log.message;
          }
          else if(log.type=="lot"){
            var lpinfo = eqpInfo.lpList.find(m => m.portId == log.portId)
            lpinfo.lotId=log.message;
          }
          else if(log.type=="Carrier"){
            var lpinfo = eqpInfo.lpList.find(m => m.portId == log.portId)
            lpinfo.carrierId=log.message;
          }
        }
      });
    },
    // 生成 25 个垂直排列的槽
    generateSlots(filledCount) {
      return Array.from({ length: 25 }, (_, i) => ({
        filled: 0,
        number: i + 1
      }));
    },
    getAllEqpList() {
      this.loading = true
      try {
        GetEqpInfo(this.pageInfo).then(res => {
          res.data.list.forEach(element => {
            element.portInfos.forEach(a=>{
              a.slots=this.generateSlots(0)
            })
            this.eqpInfos.push({
              eqpId: element.eqpId,
              eqpType: element.eqpType,
              enable: element.enable,
              eqpStatus: element.eqpStatus,
              eqpStatus: element.eapStatus,
              controlState:element.controlState,
              logs: [],
              lpList: element.portInfos
            })
          });
          this.selectedItem = this.eqpInfos[0];
        });
      } finally {
        this.loading = false
      }
    }
  },
  computed: {
    filteredList() {
      return this.eqpInfos.filter(item =>
        item.eqpId.toLowerCase().includes(this.searchQuery.toLowerCase())
      );
    },
  },
  mounted() {
    this.initSignalR()
    this.getAllEqpList()
  },
  beforeDestroy() {
    clearInterval(this.logInterval);
  }
};
</script>
  
<style scoped>
/* 外层容器布局 */
.slot-container {
  display: flex;
  width: 200px;
  height: 100%;
  /* 总宽度 = 数字列宽度 + 槽容器宽度 */
  margin-right: 15px;
}

/* 左侧数字列 */
.slot-numbers {
  height: 180px;
  width: 20px;
  padding: 5px;
}

.slot-number {
  height: 7px;
  /* 与槽位高度一致 */
  margin: 1px 5px;
  font-size: 7px;
  color: black;
  line-height: 4px;
  /* 垂直居中 */
}

/* 右侧槽位容器 */
.slot-column {
  flex: 1;
  height: 200px;
  border: 2px solid #909399;
  border-radius: 4px;
  border-top: none;
  padding: 5px;
}

/* 槽位样式 */
.slot {
  height: 7px;
  margin: 1px 0;
  color: black;
  position: relative;
}

.slot-line {
  width: 100%;
  height: 1px;
  background-color: #dcdfe9;
  position: absolute;
}

/* 同步滚动条样式 */
.slot-column::-webkit-scrollbar {
  width: 4px;
}

.slot-column::-webkit-scrollbar-thumb {
  background-color: #c0c4cc;
  border-radius: 2px;
}

/* 悬停效果 */
.slot:hover .slot-line {
  transform: scaleY(1.5);
  transition: all 0.2s;
}

.slot-row:hover .slot-number {
  color: #409EFF;
  font-weight: bold;
}

/* 滚动条样式 */
.slot-column::-webkit-scrollbar {
  width: 4px;
}

.slot-column::-webkit-scrollbar-thumb {
  background-color: #c0c4cc;
  border-radius: 2px;
}

.container {
  height: 100%;
  background: #f0f2f5;
  padding: 10px;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
}

.lp-section {
  margin-bottom: 20px;
}

.lp-card {
  margin-bottom: 20px;
  background: #f5f7fa;
}

.lp-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 15px;
}



.lp-name {
  font-size: 16px;
  font-weight: bold;
}

/* 修改 lp-content 布局 */
.lp-content {
  display: flex;
  justify-content: space-between;
  padding: 10px 0;
  background-color: re;
}

/* 单个槽样式 */


.wafer-line {
  width: 80%;
  height: 1px;
  background-color: #67c23a;
}

/* 右侧三行信息样式 */


/* 日志区域样式（保持不变） */
/* 日志框样式 */
.log-section {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.log-box {
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
}

.log-header {
  font-size: 18px;
  font-weight: bold;
  text-align: left;
  padding: 5px;
  border-bottom: 1px solid #ebeef5;
  height: 40px;
  box-sizing: border-box;
}

.log-container {
  flex-grow: 1;
  padding: 10px;
  min-height: 400px;
  max-height: 400px;
  text-align: left;
  overflow-y: auto;
  background: #1f2d3d;
  border-radius: 5px;
  color: #fff;
  font-family: monospace;
  box-sizing: border-box;
}

.log-item {
  padding: 5px 0;
  line-height: 1.5;
  font-size: 14px;
}

.log-time {
  color: #909399;
  margin-right: 10px;
}

.log-content {
  color: #fff;
}

/* 日志类型颜色 */
.log-item.info .log-content {
  color: #67c23a;
}

.log-item.warning .log-content {
  color: #e6a23c;
}

.log-item.error .log-content {
  color: #f56c6c;
}




.lp-info {
  display: flex;
  justify-content: space-between;
  flex-direction: column;
}

.info-tag {
  margin: 1px 0;
  text-align: left;
}

.main-wrapper {
  flex: 1;
  min-height: 0;
  /* 关键！修复 flex 容器内容溢出问题 */
  display: flex;
}

.el-col {
  display: flex;
  flex-direction: column;
}

.left-panel {
  flex: 1;
  background: white;
  border-radius: 8px;
  padding: 16px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.right-panel {
  border-radius: 8px;
  padding: 16px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  height: 100%;
  display: flex;
  flex-direction: column;
}

/* 滚动条样式 */
.list-container::-webkit-scrollbar {
  width: 6px;
}

.list-container::-webkit-scrollbar-track {
  background: #f5f5f5;
}

.list-container::-webkit-scrollbar-thumb {
  background: #c0c4cc;
  border-radius: 4px;
}

.list-container {
  flex: 1;
  overflow-y: auto;
}

.list-item {
  padding: 12px;
  margin-bottom: 8px;
  cursor: pointer;
  border-left: 4px solid transparent;
  transition: all 0.3s;
  border-radius: 4px;
  text-align: left;
}

.list-item:hover {
  background-color: #f5f7fa;
}

.active-item {
  background-color: #ecf5ff;
  border-left-color: #409eff;
}

.item-title {
  font-size: 14px;
  color: #303133;
  margin-bottom: 4px;
  font-weight: 500;
}

.item-desc {
  font-size: 12px;
  color: #909399;
  line-height: 1.4;
}
</style>