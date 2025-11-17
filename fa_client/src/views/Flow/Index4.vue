<template>
  <div class="container">
    <el-row class="main-wrapper" :gutter="10">
      <!-- 左侧列表 -->
      <el-col :span="4">
        <div class="left-panel">
          <div class="search-container">
            <el-input v-model="searchQuery" placeholder="搜索" clearable>
              <i slot="prefix" class="el-input__icon el-icon-search"></i>
            </el-input>
          </div>
        </div>
      </el-col>

      <!-- 右侧内容 -->
      <el-col :span="20">
        <div class="right-panel">
          <!-- 操作按钮 -->
          <div class="operation-buttons">
            <el-button type="primary" icon="el-icon-plus" @click="handleAdd">新建</el-button>
            <el-button type="success" icon="el-icon-edit" :disabled="!selectedNode">编辑</el-button>
            <el-button type="danger" icon="el-icon-delete" :disabled="!selectedNode">删除</el-button>
          </div>

          <!-- 流程图容器 -->
          <div class="flowchart-container">
            <svg ref="svgContainer" width="100%" :height="computedHeight">
              <defs>
                <marker v-for="color in arrowColors" :id="`arrowHead-${color}`" :key="color" markerWidth="10"
                  markerHeight="7" refX="9" refY="3.5" orient="auto">
                  <polygon :points="arrowPoints" :fill="color" />
                </marker>
              </defs>

              <!-- 连接线 -->
              <path v-for="(link, index) in links" :key="'link-' + index" :d="link.d" :stroke="link.color" fill="none"
                stroke-width="2" :marker-end="`url(#arrowHead-${link.color})`" />

              <!-- 节点 -->
              <g v-for="node in nodes" :key="'node-' + node.id" :transform="`translate(${node.x}, ${node.y})`"
                @contextmenu.prevent="showContextMenu(node, $event)">
                <circle :r="node.radius" :class="['node', node.status]" @mousedown.left="startDrag(node, $event)"
                  @mouseenter="hoverNode = node" @mouseleave="hoverNode = null" />
                <text class="node-label" x="0" y="0" dominant-baseline="middle" text-anchor="middle" dy="0.35em">
                  <!-- 添加微调偏移 -->
                  {{ node.label }}
                </text>
              </g>
            </svg>

            <!-- 右键菜单 -->
            <div v-if="contextMenu.visible" class="context-menu"
              :style="{ left: contextMenu.x + 'px', top: contextMenu.y + 'px' }">
              <div class="menu-item" @click="addNode('before')">添加上游节点</div>
              <div class="menu-item" @click="addNode('after')">添加下游节点</div>
              <div class="menu-item" @click="deleteNode">删除节点</div>
            </div>
          </div>
        </div>
      </el-col>
    </el-row>
  </div>
</template>

<script>
import { hierarchy, tree, cluster, forceSimulation, forceLink, forceManyBody, forceX, forceY } from 'd3';
import { line, curveBasis } from 'd3-shape';

export default {
  data() {
    return {
      searchQuery: "",
      nodes: [],
      links: [],
      connectionRules: {
        allowLoop: false,
        direction: 'downstream',
        nodeTypes: {
          'typeA': ['typeB', 'typeC'],
          'typeB': ['typeC']
        }
      },
      layoutConfig: {
        type: 'hierarchy', // hierarchy | force
        direction: 'LR',   // 布局方向: LR/RL/TB/BT
        spacing: {         // 间距配置
          horizontal: 180,
          vertical: 120
        },
        forceParams: {     // 力导向参数
          charge: -300,    // 节点斥力
          linkDistance: 100,// 连接线长度
          iterations: 300  // 模拟迭代次数
        }
      },
      arrowPoints: "0 0, 10 3.5, 0 7",
      arrowColors: ['#4CAF50', '#2196F3', '#9E9E9E'],
      // 布局参数
      nodeRadius: 24,
      horizontalSpacing: 200,
      verticalSpacing: 150,
      // 交互状态
      dragging: false,
      dragNode: null,
      startPos: { x: 0, y: 0 },
      hoverNode: null,
      contextMenu: {
        visible: false,
        x: 0,
        y: 0,
        node: null
      }
    };
  },

  computed: {
    computedHeight() {
      return Math.max(600, this.nodes.length * 50);
    }
  },

  methods: {
    initSampleData() {
      this.nodes = Array.from({ length: 12 }, (_, i) => ({
        id: i + 1,
        label: `${Math.floor(Math.random() * 90 + 10)}`,
        status: ['pending', 'current', 'completed'][i % 3],
        radius: 24,
        x: 0,
        y: 0
      }));
      this.updateLayout();
    },

    updateLayout() {
      this.calculateNodePositions();
      this.calculateLinks();
    },

    calculateNodePositions() {
      const nodesPerRow = 6;
      this.nodes.forEach((node, index) => {
        const row = Math.floor(index / nodesPerRow);
        const col = index % nodesPerRow;

        node.x = 120 + col * this.horizontalSpacing;
        node.y = 100 + row * this.verticalSpacing;

        // 交错排列
        if (row % 2 === 1) {
          node.x += this.horizontalSpacing / 2;
        }
      });
    },

    calculateLinks() {
      const lineGenerator = line()
        .x(d => d.x)
        .y(d => d.y)
        .curve(curveBasis);

      this.links = this.nodes.slice(1).map((node, i) => {
        const source = this.nodes[i];
        const target = node;

        const angle = Math.atan2(target.y - source.y, target.x - source.x);
        return {
          d: lineGenerator([
            {
              x: source.x + source.radius * Math.cos(angle),
              y: source.y + source.radius * Math.sin(angle)
            },
            {
              x: target.x - target.radius * Math.cos(angle),
              y: target.y - target.radius * Math.sin(angle)
            }
          ]),
          color: this.getLinkColor(source.status)
        };
      });
    },

    getLinkColor(status) {
      const colors = {
        completed: '#4CAF50',
        current: '#2196F3',
        pending: '#9E9E9E'
      };
      return colors[status] || '#999';
    },

    // 节点交互
    startDrag(node, event) {
      this.dragging = true;
      this.dragNode = node;
      this.startPos = {
        x: event.clientX - node.x,
        y: event.clientY - node.y
      };

      document.addEventListener('mousemove', this.dragMove);
      document.addEventListener('mouseup', this.endDrag);
    },

    dragMove(event) {
      if (!this.dragging) return;

      this.dragNode.x = event.clientX - this.startPos.x;
      this.dragNode.y = event.clientY - this.startPos.y;
      this.calculateLinks();
    },

    endDrag() {
      this.dragging = false;
      document.removeEventListener('mousemove', this.dragMove);
      document.removeEventListener('mouseup', this.endDrag);
      this.saveLayout();
    },

    // 上下文菜单
    showContextMenu(node, event) {
      this.contextMenu = {
        visible: true,
        x: event.clientX,
        y: event.clientY,
        node: node
      };
    },

    // 修改节点添加方法
    addNode(position) {
      const newNode = {
        id: Date.now(),
        label: '新节点',
        type: 'default',
        status: 'pending',
        radius: 24,
        x: 0,
        y: 0
      };

      const referenceNode = this.contextMenu.node;
      const referenceIndex = this.nodes.findIndex(n => n.id === referenceNode.id);

      // 计算新节点位置
      if (position === 'after') {
        this.nodes.splice(referenceIndex + 1, 0, newNode);
        this.createConnection(referenceNode, newNode);
      } else {
        this.nodes.splice(referenceIndex, 0, newNode);
        this.createConnection(newNode, referenceNode);
      }

      this.updateLayout();
    },

    deleteNode() {
      this.nodes = this.nodes.filter(n => n.id !== this.contextMenu.node.id);
      this.updateLayout();
      this.closeContextMenu();
    },

    closeContextMenu() {
      this.contextMenu.visible = false;
    },

    saveLayout() {
      // 保存布局逻辑
    },


    // 新增连接验证方法
    validateConnection(source, target) {
      // 1. 检查自连接
      if (source.id === target.id) {
        this.showError('不能连接相同节点');
        return false;
      }

      // 2. 检查重复连接
      if (this.isDuplicateConnection(source, target)) {
        this.showError('已存在相同连接');
        return false;
      }

      // 3. 检查连接方向
      if (!this.checkDirection(source, target)) {
        this.showError('违反连接方向规则');
        return false;
      }

      // 4. 检查节点类型
      if (!this.checkNodeTypes(source, target)) {
        this.showError('节点类型不匹配');
        return false;
      }

      // 5. 检查环路形成
      if (this.connectionRules.allowLoop === false &&
        this.wouldCreateCycle(source, target)) {
        this.showError('连接将导致环路');
        return false;
      }

      return true;
    },

    // 辅助验证方法
    isDuplicateConnection(source, target) {
      return this.links.some(link =>
        link.sourceId === source.id &&
        link.targetId === target.id
      );
    },

    checkDirection(source, target) {
      switch (this.connectionRules.direction) {
        case 'any':
          return true;
        case 'downstream':
          return target.x > source.x;
        case 'upstream':
          return target.x < source.x;
        default:
          return true;
      }
    },

    checkNodeTypes(source, target) {
      const allowedTypes = this.connectionRules.nodeTypes[source.type];
      return allowedTypes ? allowedTypes.includes(target.type) : true;
    },

    wouldCreateCycle(source, target) {
      const adjacency = new Map();
      this.links.forEach(({ sourceId, targetId }) => {
        if (!adjacency.has(sourceId)) adjacency.set(sourceId, []);
        adjacency.get(sourceId).push(targetId);
      });

      // 添加待检查的新连接
      if (!adjacency.has(source.id)) adjacency.set(source.id, []);
      adjacency.get(source.id).push(target.id);

      const visited = new Set();
      const stack = new Set();

      const hasCycle = (nodeId) => {
        if (stack.has(nodeId)) return true;
        if (visited.has(nodeId)) return false;

        visited.add(nodeId);
        stack.add(nodeId);

        const neighbors = adjacency.get(nodeId) || [];
        for (const neighborId of neighbors) {
          if (neighborId === source.id || hasCycle(neighborId)) {
            return true;
          }
        }

        stack.delete(nodeId);
        return false;
      };

      return hasCycle(target.id);
    },

    showError(message) {
      this.$message({
        message,
        type: 'error',
        duration: 2000
      });
    },

    // 修改后的连接创建方法
    createConnection(source, target) {
      if (!this.validateConnection(source, target)) return;

      const newLink = {
        sourceId: source.id,
        targetId: target.id,
        color: this.getLinkColor(source.status),
        d: this.generateLinkPath(source, target)
      };

      this.links.push(newLink);
      this.$nextTick(() => {
        this.updateLinksPath();
      });
    },

    // 更新所有连接路径
    updateLinksPath() {
      this.links = this.links.map(link => {
        const source = this.nodes.find(n => n.id === link.sourceId);
        const target = this.nodes.find(n => n.id === link.targetId);
        return {
          ...link,
          d: this.generateLinkPath(source, target)
        };
      });
    },


    // 自动布局入口方法
    autoLayout() {
      switch (this.layoutConfig.type) {
        case 'hierarchy':
          this.runHierarchyLayout();
          break;
        case 'force':
          this.runForceDirectedLayout();
          break;
      }
      this.updateLinksPath();
    },

    // 层次布局算法
    runHierarchyLayout() {
      // 1. 构建节点层级关系
      const root = this.buildHierarchyTree();

      // 2. 选择布局算法
      const layout = this.layoutConfig.direction === 'TB' ?
        cluster().size([800, 600]) :
        tree().nodeSize([this.layoutConfig.spacing.horizontal,
        this.layoutConfig.spacing.vertical]);

      // 3. 执行布局计算
      layout(root);

      // 4. 更新节点坐标
      root.descendants().forEach((node, i) => {
        const vueNode = this.nodes[i];
        vueNode.x = node.x;
        vueNode.y = node.y;
      });
    },

    // 构建树形数据结构
    buildHierarchyTree() {
      const nodeMap = new Map(this.nodes.map(n => [n.id, n]));
      const rootNodes = this.nodes.filter(n => !this.links.some(l => l.targetId === n.id));

      const buildTree = (node) => {
        const children = this.links
          .filter(l => l.sourceId === node.id)
          .map(l => nodeMap.get(l.targetId));
        return {
          id: node.id,
          children: children.map(buildTree)
        };
      };

      return hierarchy(rootNodes.map(buildTree)[0]);
    },
  },

  mounted() {
    this.initSampleData();
    window.addEventListener('click', this.closeContextMenu);
    // // 切换布局类型
    // this.layoutConfig.type = 'force';

    // // 修改力导向参数
    // this.layoutConfig.forceParams = {
    //   charge: -200,
    //   linkDistance: 150,
    //   iterations: 500
    // };
    //this.autoLayout();
  },

  beforeDestroy() {
    window.removeEventListener('click', this.closeContextMenu);
  },
  // 力导向布局算法
  runForceDirectedLayout() {
    const simulation = forceSimulation(this.nodes)
      .force("link", forceLink(this.links))
      .force("charge", forceManyBody().strength(this.layoutConfig.forceParams.charge))
      .force("x", forceX().strength(0.1))
      .force("y", forceY().strength(0.1))

    // 配置连接力
    simulation.force("link")
      .id(d => d.id)
      .distance(this.layoutConfig.forceParams.linkDistance)
      .links(this.links)

    // 运行动画
    simulation
      .stop()
      .tick(this.layoutConfig.forceParams.iterations),

      // 手动停止模拟
      simulation.stop()
  },

  // 布局配置界面
  layoutControls() {
    return (
      <div class="layout-controls">
        <el-select v-model={this.layoutConfig.type}>
          <el-option label="层次布局" value="hierarchy" />
          <el-option label="力导向布局" value="force" />
        </el-select>

        {this.layoutConfig.type === 'hierarchy' && (
          <el-select v-model={this.layoutConfig.direction}>
            <el-option label="左→右" value="LR" />
            <el-option label="右→左" value="RL" />
            <el-option label="上→下" value="TB" />
            <el-option label="下→上" value="BT" />
          </el-select>
        )}

        <el-button type="primary" onClick={this.autoLayout}>
          应用布局
        </el-button>
      </div>
    );
  }
};
</script>

<style scoped>
.container {
  height: 100%;
  background: #f0f2f5;
  padding: 16px;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
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
  background: white;
  border-radius: 8px;
  padding: 16px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  height: 100%;
  display: flex;
  flex-direction: column;
}

.operation-buttons {
  margin-bottom: 16px;
}

.flowchart-container {
  flex: 1;
  position: relative;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  overflow: auto;
  background: #f8f9fa;
}

.node {
  stroke-width: 2;
  stroke: white;
  transition: all 0.2s;
  cursor: grab;

  &:active {
    cursor: grabbing;
  }

  &.completed {
    fill: #4CAF50;
  }

  &.current {
    fill: #2196F3;
  }

  &.pending {
    fill: #9E9E9E;
  }

  &:hover {
    filter: brightness(1.1);
    transform: scale(1.05);
  }
}

.node-label {
  fill: white;
  font-size: 14px;
  font-weight: 500;
  user-select: none;
  pointer-events: none;
  /* 添加以下属性确保跨浏览器一致性 */
  alignment-baseline: central;
  text-anchor: middle;
}

.context-menu {
  position: fixed;
  background: white;
  border-radius: 4px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.15);
  z-index: 1000;
  padding: 8px 0;
  min-width: 160px;
}

.menu-item {
  padding: 8px 16px;
  font-size: 14px;
  color: #606266;
  cursor: pointer;
  transition: all 0.2s;

  &:hover {
    background: #f5f7fa;
    color: #409eff;
  }
}

/* 布局控制样式 */
.layout-controls {
  position: absolute;
  top: 20px;
  right: 20px;
  z-index: 100;
  background: rgba(255, 255, 255, 0.9);
  padding: 12px;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  display: flex;
  gap: 8px;
}

/* 力导向动画过渡 */
.node,
.connection-line {
  transition: all 0.5s ease-out;
}
</style>