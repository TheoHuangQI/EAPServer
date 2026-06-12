<template>
  <vue-mermaid
      v-for="(data,index) in flowList"
      :key="index"
      :id="`flow-chart-${index}`"
      :nodes="data"
      type="graph LR" />
  </div>
  </template>
  
  <script>
  import { line, curveMonotoneX } from 'd3-shape';
  
  export default {
    data() {
      return {
        searchQuery: "",
        selectedItem: null,
        arrowPoints: "0 0, 10 3.5, 0 7",
        currentLinkColor: '#999',
        nodes: [], // 节点数据
        links: [], // 连接线数据\
        // 修改节点尺寸参数
        nodeRadius: 30,       // 圆形半径
        horizontalSpacing: 180,  // 增加水平间距
        verticalSpacing: 120,    // 增加垂直间距
        selectedNode: null,
        showMenu: false,
        menuX: 0,
        menuY: 0,
        rows: 0,
        dynamicRadius: false,
        dragging: false,
        dragNode: null,
        startX: 0,
        startY: 0
  
      };
    },
  
    computed: {
      computedHeight() {
        return this.rows * this.verticalSpacing + 100;
      },
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
      selectItem(item) {
        this.selectedItem = item;
        this.$nextTick(() => {
          this.updateChart();
        });
      },
  
      initSampleData() {
        // 初始化示例节点
        this.nodes = [
          { id: 1, label: '14', status: 'completed' },
          { id: 2, label: '72', status: 'current' },
          { id: 3, label: '32', status: 'pending' },
          { id: 4, label: '59', status: 'pending' },
          { id: 1, label: '14', status: 'pending' },
          { id: 2, label: '72', status: 'pending' },
          { id: 3, label: '32', status: 'pending' },
          { id: 4, label: '59', status: 'pending' },
          { id: 1, label: '14', status: 'pending' },
          { id: 2, label: '72', status: 'pending' },
          { id: 3, label: '32', status: 'pending' },
          { id: 4, label: '59', status: 'pending' },
          { id: 1, label: '14', status: 'pending' },
          { id: 2, label: '72', status: 'pending' },
          { id: 3, label: '32', status: 'pending' },
          { id: 4, label: '59', status: 'pending' },
          { id: 1, label: '14', status: 'pending' },
          { id: 2, label: '72', status: 'pending' },
          { id: 3, label: '32', status: 'pending' },
          { id: 4, label: '59', status: 'pending' },
        ];
        this.updateLayout();
      },
  
      updateLayout() {
        // 计算节点位置和连接线
        this.calculateNodePositions();
        this.calculateLinks();
      },
      calculateNodePositions() {
        const nodesPerRow = 8;
        this.rows = Math.ceil(this.nodes.length / nodesPerRow);
  
        this.nodes.forEach((node, index) => {
          const row = Math.floor(index / nodesPerRow);
          const col = index % nodesPerRow;
  
          // 计算圆心坐标
          const xBase = (row % 2 === 0)
            ? col * this.horizontalSpacing
            : (nodesPerRow - col - 1) * this.horizontalSpacing;
  
          node.x = xBase + this.nodeRadius + 50;  // 添加边距
          node.y = row * this.verticalSpacing + 100; // 纵向位置调整
        });
      },
      calculateLinks() {
        this.links = [];
        const lineGenerator = line()
          .x(d => d.x)
          .y(d => d.y)
          .curve(curveMonotoneX);
  
        for (let i = 1; i < this.nodes.length; i++) {
          const prev = this.nodes[i - 1];
          const current = this.nodes[i];
  
          // 计算圆形边缘坐标
          const angle = Math.atan2(
            current.y - prev.y,
            current.x - prev.x
          );
  
          const startPoint = {
            x: prev.x + this.nodeRadius * Math.cos(angle),
            y: prev.y + this.nodeRadius * Math.sin(angle)
          };
  
          const endPoint = {
            x: current.x - this.nodeRadius * Math.cos(angle),
            y: current.y - this.nodeRadius * Math.sin(angle)
          };
  
          this.links.push({
            d: lineGenerator([startPoint, endPoint]),
            color: this.getLinkColor(prev.status)
          });
        }
      },
      // 新增颜色判断方法
      getLinkColor(status) {
        switch (status) {
          case 'completed':
            return '#4CAF50'; // 已完成-绿色
          case 'current':
            return '#2196F3'; // 当前-蓝色
          default:
            return '#9E9E9E'; // 未完成-灰色
        }
      },
      handleNodeClick(node, event) {
        this.selectedNode = node;
        this.showMenu = true;
        this.menuX = event.pageX;
        this.menuY = event.pageY;
      },
      addNode(position) {
        const newNodeId = Date.now();
        const newNode = {
          id: newNodeId,
          label: `步骤${newNodeId}`,
          status: 'pending',
        };
  
        const index = this.nodes.findIndex(n => n.id === this.selectedNode.id);
        if (position === 'before' && index > 0) {
          this.nodes.splice(index, 0, newNode);
        } else {
          this.nodes.splice(index + 1, 0, newNode);
        }
  
        this.updateLayout();
        this.showMenu = false;
      },
      // 添加调整方法
      adjustRadius(scale) {
        this.nodes.forEach(node => {
          node.radius = 40 * scale;
        });
      },
      startDrag(node, event) {
        this.dragging = true;
        this.dragNode = node;
        this.startX = event.clientX;
        this.startY = event.clientY;
  
        document.addEventListener('mousemove', this.dragMove);
        document.addEventListener('mouseup', this.dragEnd);
      },
      dragMove(event) {
        if (!this.dragging) return;
  
        const dx = event.clientX - this.startX;
        const dy = event.clientY - this.startY;
  
        // 更新节点位置
        this.dragNode.x += dx;
        this.dragNode.y += dy;
  
        // 实时更新连接线
        this.calculateLinks();
  
        this.startX = event.clientX;
        this.startY = event.clientY;
      },
      dragEnd() {
        this.dragging = false;
        document.removeEventListener('mousemove', this.dragMove);
        document.removeEventListener('mouseup', this.dragEnd);
  
        // 触发布局保存逻辑
        this.saveLayoutToLocalStorage();
      },
      saveLayoutToLocalStorage() {
        const layoutData = this.nodes.map(node => ({
          id: node.id,
          x: node.x,
          y: node.y
        }));
        localStorage.setItem('flowchartLayout', JSON.stringify(layoutData));
      },
      createColorArrows() {
        const colors = ['#4CAF50', '#2196F3', '#9E9E9E'];
        colors.forEach(color => {
          const marker = document.createElementNS('http://www.w3.org/2000/svg', 'marker');
          marker.id = `arrowHead-${color}`;
          marker.setAttribute('markerWidth', '10');
          marker.setAttribute('markerHeight', '7');
          marker.setAttribute('refX', '9');
          marker.setAttribute('refY', '3.5');
          marker.setAttribute('orient', 'auto');
  
          const polygon = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
          polygon.setAttribute('points', this.arrowPoints);
          polygon.setAttribute('fill', color);
  
          marker.appendChild(polygon);
          this.$el.querySelector('defs').appendChild(marker);
        });
      },
    },
  
    mounted() {
      this.initSampleData();
      this.$nextTick(() => {
        this.createColorArrows();
      });
    },
  
    beforeUnmount() {
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
    background-color:re;
    box-sizing: border-box;
    /* 关键修复 */
    padding: 5px;
    display: flex;
    flex-direction: column;
  
  }
  </style>