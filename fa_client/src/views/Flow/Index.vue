<template>
  <div class="container">
    <el-row class="main-wrapper" :gutter="10">
      <!-- 左侧列表 -->
      <el-col :span="3">
        <div class="left-panel">
          <div class="search-container">
            <el-input
              v-model="searchQuery"
              placeholder="搜索"
              prefix-icon="el-icon-search"
              clearable
            />
          </div>
          <div class="list-container">
            <div
              v-for="(item, index) in filteredList"
              :key="index"
              class="list-item"
              :class="{ 'active-item': selectedItem === item }"
              @click="selectItem(item)"
            >
              <div class="item-content">
                <div class="item-title">{{ item.name }}</div>
              </div>
            </div>
          </div>
        </div>
      </el-col>

      <!-- 右侧内容 -->
      <el-col :span="21">
        <div class="right-panel">
          <!-- 操作按钮 -->
          <div class="operation-buttons">
            <el-button type="primary" icon="el-icon-plus" @click="handleAdd"
              >添加</el-button
            >
            <el-button
              type="success"
              icon="el-icon-edit"
              :disabled="!selectedItem"
              @click="handleEdit"
              >修改</el-button
            >
            <el-button
              type="danger"
              icon="el-icon-delete"
              :disabled="!selectedItem"
              @click="handleDelete"
              >删除</el-button
            >
          </div>

          <!-- 图表区域 -->
          <div class="chart-container">
            <div ref="chartContainer" class="chart-content"></div>
          </div>
        </div>
      </el-col>
    </el-row>
  </div>
</template>

<script>
import * as echarts from "echarts";

export default {
  data() {
    return {
      searchQuery: "",
      selectedItem: null,
      list: [
        {
          name: "完整事件时序",
          data: [150, 230, 224, 218, 135, 147, 260],
        },
        {
          name: "E40状态时序",
          data: [120, 200, 150, 80, 70, 110, 130],
        },
        {
          name: "E87状态时序",
          data: [320, 332, 301, 334, 390, 330, 320],
        },
        {
          name: "E90状态时序",
          data: [820, 932, 901, 934, 1290, 1330, 1320],
        },
        {
          name: "Substrate位置时序",
          data: [50, 232, 201, 154, 190, 330, 410],
        },
      ],
      chart: null,
    };
  },

  computed: {
    filteredList() {
      const query = this.searchQuery.toLowerCase();
      return this.list.filter(
        (item) =>
          item.name.toLowerCase().includes(query) ||
          item.description.toLowerCase().includes(query)
      );
    },
  },

  methods: {
    initChart() {
      if (this.chart) {
        this.chart.dispose();
      }
      this.chart = echarts.init(this.$refs.chartContainer);
    },

    updateChart() {
      if (!this.selectedItem) return;

      const option = {
        animation: false,
        grid: {
          top: 40,
          right: 40,
          bottom: 40,
          left: 60,
        },
        tooltip: {
          trigger: "axis",
        },
        xAxis: {
          type: "category",
          data: ["周一", "周二", "周三", "周四", "周五", "周六", "周日"],
          axisLine: {
            lineStyle: {
              color: "#ddd",
            },
          },
        },
        yAxis: {
          type: "value",
          axisLine: {
            lineStyle: {
              color: "#ddd",
            },
          },
          splitLine: {
            lineStyle: {
              color: "#eee",
            },
          },
        },
        series: [
          {
            name: this.selectedItem.name,
            type: "line",
            smooth: true,
            data: this.selectedItem.data,
            itemStyle: {
              color: "#3b82f6",
            },
            areaStyle: {
              color: {
                type: "graph",
                x: 0,
                y: 0,
                x2: 0,
                y2: 1,
                colorStops: [
                  {
                    offset: 0,
                    color: "rgba(59,130,246,0.2)",
                  },
                  {
                    offset: 1,
                    color: "rgba(59,130,246,0.01)",
                  },
                ],
              },
            },
          },
        ],
      };

      this.chart.setOption(option);
    },

    selectItem(item) {
      this.selectedItem = item;
      this.$nextTick(() => {
        this.updateChart();
      });
    },

    handleAdd() {
      this.$message({
        message: "点击了添加按钮",
        type: "info",
      });
    },

    handleEdit() {
      if (!this.selectedItem) return;
      this.$message({
        message: `准备编辑：${this.selectedItem.name}`,
        type: "warning",
      });
    },

    handleDelete() {
      if (!this.selectedItem) return;
      this.$confirm(`确定要删除【${this.selectedItem.name}】吗？`, "提示", {
        confirmButtonText: "确定",
        cancelButtonText: "取消",
        type: "warning",
      })
        .then(() => {
          const index = this.list.findIndex(
            (item) => item === this.selectedItem
          );
          this.list.splice(index, 1);
          this.selectedItem = null;
          this.$message.success("删除成功");
        })
        .catch(() => {});
    },

    handleResize() {
      this.chart && this.chart.resize();
    },
  },

  mounted() {
    this.initChart();
    if (this.list.length > 0) {
      this.selectItem(this.list[0]);
    }
    window.addEventListener("resize", this.handleResize);
  },

  beforeDestroy() {
    window.removeEventListener("resize", this.handleResize);
    if (this.chart) {
      this.chart.dispose();
    }
  },
};
</script>

<style scoped>
.container {
  height: 100%;
  background-color: #f5f7fa;
  box-sizing: border-box; /* 关键修复 */
  padding: 5px;
  display: flex;
  flex-direction: column;

}

.main-wrapper {
  flex: 1;
  min-height: 0; /* 关键！修复 flex 容器内容溢出问题 */
  display: flex;
}
/* 确保列高度继承 */
.el-col {
  display: flex;
  flex-direction: column;
}

.left-panel {
  flex: 1;
  min-height: 0; /* 允许内容收缩 */
  height: 100%;
  background: white;
  padding: 15px;
  border-radius: 4px;
  box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
}

.search-container {
  margin-bottom: 15px;
}

.list-container {
  flex: 1;
  min-height: 0; /* 重要：允许滚动 */
  overflow-y: auto;
 
}

.list-item {
  padding: 12px;
  margin-bottom: 8px;
  cursor: pointer;
  border-left: 4px solid transparent;
  transition: all 0.3s;
  border-radius: 4px;
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

.right-panel {
  flex: 1;
  min-height: 0; /* 允许内容收缩 */
  height: 100%;
  padding: 20px;
  background: white;
  border-radius: 4px;
  box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
}

.operation-buttons {
  text-align:left;
  margin-bottom: 20px;
}

.operation-buttons .el-button {
  padding: 10px 20px;
}

.chart-container {
  height:100% ;
}

.chart-content {
  width: 100%;
  height: 100%;
  background: white;
  border-radius: 4px;
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
</style>