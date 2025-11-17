<template>
  <div class="app-container">
    <!-- 顶部工具栏 -->
    <el-row class="top-bar" type="flex" align="middle">
      <el-col :span="12">
        <span class="label">流程名：</span>
        <el-input v-model="filename" style="width: 300px"></el-input>
      </el-col>
      <el-col :span="12" class="text-right">
        <el-button type="primary">保存</el-button>
        <el-button>取消</el-button>
      </el-col>
    </el-row>

    <div class="main-content">
      <!-- 左侧工具栏 -->
      <div class="left-toolbar">
        <div 
          v-for="(node, index) in nodeTypes"
          :key="index"
          class="node-template"
          draggable="true"
          @dragstart="handleDragStart($event, node)"
        >
          <div class="node-icon" :style="node.style">
            <i :class="node.icon"></i>
          </div>
          <span class="node-label">{{ node.label }}</span>
        </div>
      </div>

       <!-- 画布区域 -->
       <div 
        class="canvas-area"
        @dragover.prevent
        @drop="handleDrop"
        ref="canvas"
      >
        <svg class="flow-canvas" :width="canvasWidth" :height="canvasHeight">
          <!-- 连线 -->
          <path 
            v-for="(link, index) in links"
            :key="'link'+index"
            :d="calculateLinkPath(link)"
            stroke="#999"
            fill="none"
            stroke-width="2"
          />
          
          <!-- 节点 -->
          <foreignObject 
            v-for="node in nodes"
            :key="node.id"
            :x="node.x" 
            :y="node.y"
            :width="nodeWidth"
            :height="nodeHeight"
            class="flow-node"
            @mousedown="startDrag(node, $event)"
          >
            <div 
              class="node-body"
              :style="getNodeStyle(node.type)"
              @mouseup="stopDrag"
            >
              <div class="node-content">
                <i :class="getNodeIcon(node.type)"></i>
                {{ node.name }}
              </div>
            </div>
          </foreignObject>
        </svg>
      </div>

      <!-- 右侧属性栏 -->
      <div class="right-property">
        <el-form label-width="80px" v-if="selectedNode">
          <el-form-item label="节点名称">
            <el-input v-model="selectedNode.name"></el-input>
          </el-form-item>
          <el-form-item label="审批类型">
            <el-input value="用户" disabled></el-input>
          </el-form-item>
          <el-form-item label="审批人">
            <el-select placeholder="请选择审批人">
              <el-option label="张三" value="2000029"></el-option>
              <el-option label="李四" value="2000030"></el-option>
              <el-option label="王五" value="2100175"></el-option>
            </el-select>
          </el-form-item>
        </el-form>
        <div v-else class="empty-tip">请选择节点</div>
      </div>
    </div>
  </div>
</template>

<script>
import mermaid from 'mermaid';

export default {
  data() {
    return {
      filename: '单Foup作业',
      nodes: [
        // 初始测试数据
        { id: 'node1', type: 'start', name: '开始', x: 100, y: 100 },
        { id: 'node2', type: 'approve', name: '过程节点', x: 300, y: 100 }
      ],
      links: ['node1-->node2'],
      selectedNode: null,
      nodeTypes: [
        {
          type: 'start',
          label: '开始节点',
          icon: 'el-icon-caret-right',
          style: { backgroundColor: '#4CAF50' }
        },
        {
          type: 'end',
          label: '结束节点',
          icon: 'el-icon-switch-button',
          style: { backgroundColor: '#EF5350' }
        },
        {
          type: 'approve',
          label: '过程节点',
          icon: 'el-icon-s-check',
          style: { 
            backgroundColor: '#E3F2FD',
            border: '2px solid #90CAF9',
            borderRadius: '8px'
          }
        },
        {
          type: 'condition',
          label: '条件节点',
          icon: 'el-icon-question',
          style: { 
            backgroundColor: '#FFF8E1', 
            border: '2px solid #FFE082',
            transform: 'rotate(45deg)',
            borderRadius: '8px'
          }
        }
      ],
      draggingNode: null,
      nodeWidth: 120,
      nodeHeight: 60,
      canvasWidth: 2000,
      canvasHeight: 2000,
      offset: { x: 0, y: 0 }
    };
  },
  mounted() {
    mermaid.initialize({ 
      startOnLoad: false,
      securityLevel: 'loose',
      theme: 'neutral'
    });
    this.updateDiagram();
  },
  methods: {
    // 拖拽开始处理
    handleDragStart(e, nodeType) {
      const nodeId = `node_${Date.now()}`;
      e.dataTransfer.setData('text/plain', JSON.stringify({
        type: nodeType.type,
        id: nodeId,
        name: nodeType.label
      }));
    },
    // 放置处理
    handleDrop(e) {
      const data = JSON.parse(e.dataTransfer.getData('text/plain'));
      const rect = this.$refs.canvas.getBoundingClientRect();
      
      const newNode = {
        id: data.id,
        type: data.type,
        name: data.name,
        x: e.clientX - rect.left - this.nodeWidth/2,
        y: e.clientY - rect.top - this.nodeHeight/2
      };

      // 自动连接逻辑
      if (this.nodes.length > 0) {
        const lastNode = this.nodes[this.nodes.length - 1];
        this.links.push({
          source: lastNode.id,
          target: newNode.id
        });
      }

      this.nodes.push(newNode);
    },
    // 开始拖拽节点
    startDrag(node, e) {
      this.draggingNode = node;
      const rect = e.target.getBoundingClientRect();
      this.offset = {
        x: e.clientX - rect.left,
        y: e.clientY - rect.top
      };
      
      document.addEventListener('mousemove', this.handleDrag);
      document.addEventListener('mouseup', this.stopDrag);
    },
 // 处理拖拽移动
 handleDrag(e) {
      if (!this.draggingNode) return;
      
      const rect = this.$refs.canvas.getBoundingClientRect();
      this.draggingNode.x = e.clientX - rect.left - this.offset.x;
      this.draggingNode.y = e.clientY - rect.top - this.offset.y;
      
      // 更新连线位置
      this.$forceUpdate();
    },
 // 停止拖拽
 stopDrag() {
      this.draggingNode = null;
      document.removeEventListener('mousemove', this.handleDrag);
      document.removeEventListener('mouseup', this.stopDrag);
    },
    // 计算连线路径
    calculateLinkPath(link) {
      const sourceNode = this.nodes.find(n => n.id === link.source);
      const targetNode = this.nodes.find(n => n.id === link.target);
      
      if (!sourceNode || !targetNode) return '';
      
      const start = {
        x: sourceNode.x + this.nodeWidth/2,
        y: sourceNode.y + this.nodeHeight
      };
      
      const end = {
        x: targetNode.x + this.nodeWidth/2,
        y: targetNode.y
      };
      
      // 贝塞尔曲线路径
      return `M ${start.x} ${start.y} 
              C ${start.x} ${start.y + 50}, 
                ${end.x} ${end.y - 50}, 
                ${end.x} ${end.y}`;
    },
     // 获取节点样式
     getNodeStyle(type) {
      const styles = {
        start: {
          background: '#4CAF50',
          borderRadius: '30px'
        },
        end: {
          background: '#EF5350',
          borderRadius: '30px'
        },
        approve: {
          background: '#E3F2FD',
          border: '2px solid #90CAF9',
          borderRadius: '8px'
        },
        condition: {
          background: '#FFF8E1',
          border: '2px solid #FFE082',
          transform: 'rotate(45deg)',
          borderRadius: '8px'
        }
      };
      return styles[type] || {};
    },
    // 获取节点图标
    getNodeIcon(type) {
      const icons = {
        start: 'el-icon-caret-right',
        end: 'el-icon-switch-button',
        approve: 'el-icon-s-check',
        condition: 'el-icon-question'
      };
      return icons[type] || '';
    },
    updateDiagram() {
      this.$nextTick(() => {
        const diagram = this.$refs.mermaid;
        const mermaidCode = this.generateMermaidCode();
        
        mermaid.render('mermaid-svg', mermaidCode, (svgCode) => {
          diagram.innerHTML = svgCode;
          this.addNodeListeners();
        });
      });
    },
    generateMermaidCode() {
      let code = 'graph TD;\n';
      
      // 添加节点样式
      this.nodes.forEach(node => {
        let style = '';
        switch(node.type) {
          case 'start':
            style = 'fill:#4CAF50,stroke:#388E3C,color:white';
            break;
          case 'end':
            style = 'fill:#EF5350,stroke:#D32F2F,color:white';
            break;
          case 'approve':
            style = 'fill:#E3F2FD,stroke:#90CAF9,color:#333';
            break;
          case 'condition':
            style = 'fill:#FFF8E1,stroke:#FFE082,color:#333';
            break;
        }
        code += `${node.id}["${node.name}"]:::${node.type};\n`;
      });

      // 添加连线
      this.links.forEach(link => {
        code += `${link};\n`;
      });

      // 添加样式定义
      code += `
        classDef start ${styleToCss('start')};
        classDef end ${styleToCss('end')};
        classDef approve ${styleToCss('approve')};
        classDef condition ${styleToCss('condition')};
      `;

      function styleToCss(type) {
        const styles = {
          start: { fill: '#4CAF50', stroke: '#388E3C', color: 'white' },
          end: { fill: '#EF5350', stroke: '#D32F2F', color: 'white' },
          approve: { fill: '#E3F2FD', stroke: '#90CAF9' },
          condition: { fill: '#FFF8E1', stroke: '#FFE082' }
        };
        return Object.entries(styles[type])
          .map(([k, v]) => `${k}:${v}`)
          .join(',');
      }

      return code;
    },
    addNodeListeners() {
      const nodes = this.$refs.mermaid.querySelectorAll('.node');
      nodes.forEach(node => {
        node.addEventListener('click', (e) => {
          const nodeId = node.id.replace('node-', '');
          this.selectedNode = this.nodes.find(n => n.id === nodeId);
        });
      });
    }
  }
};
</script>

<style scoped>
.flow-canvas {
  background: white;
  border-radius: 4px;
  box-shadow: 0 2px 12px 0 rgba(0,0,0,.1);
}

.flow-node {
  cursor: move;
  user-select: none;
}

.node-body {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: box-shadow 0.3s;
}

.node-body:hover {
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

.node-content {
  text-align: center;
  padding: 8px;
}

.node-content i {
  display: block;
  font-size: 20px;
  margin-bottom: 4px;
}

/* 条件节点特殊处理 */
[style*="rotate(45deg)"] .node-content {
  transform: rotate(-45deg);
}

.app-container {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.top-bar {
  padding: 12px;
  background: white;
  border-bottom: 1px solid #ebeef5;
}

.main-content {
  flex: 1;
  display: flex;
  min-height: 0;
}

.left-toolbar {
  width: 200px;
  padding: 20px;
  background: white;
  border-right: 1px solid #ebeef5;
  overflow-y: auto;
}

.node-template {
  margin-bottom: 20px;
  text-align: center;
  cursor: move;
}

.node-icon {
  width: 60px;
  height: 60px;
  margin: 0 auto 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  color: white;
  border-radius: 8px;
}

.rotated-icon {
  transform: rotate(45deg);
}

.rotated-icon i {
  transform: rotate(-45deg);
}

.node-label {
  font-size: 12px;
  color: #606266;
}

.canvas-area {
  flex: 1;
  padding: 20px;
  background: #f5f7fa;
  overflow: auto;
}

.mermaid-diagram {
  min-width: 800px;
  min-height: 600px;
  background: white;
  border-radius: 4px;
  box-shadow: 0 2px 12px 0 rgba(0,0,0,.1);
  padding: 20px;
}

.right-property {
  width: 300px;
  padding: 20px;
  background: white;
  border-left: 1px solid #ebeef5;
}

.empty-tip {
  color: #909399;
  text-align: center;
  padding: 20px;
}
</style>