<template>
    <!-- 流程图容器 -->
    <div>
        <svg width="100%" :height="computedHeight">
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
</template>

<script>
import { hierarchy, tree, cluster, forceSimulation, forceLink, forceManyBody, forceX, forceY } from 'd3';
import { line, curveBasis } from 'd3-shape';
export default {
    data() {
        return {
            nodes: [],
            links: [],
            arrowColors: ['#4CAF50', '#2196F3', '#9E9E9E'],
            arrowPoints: "0 0, 10 3.5, 0 7",
            startPos: { x: 0, y: 0 },
            hoverNode: null,
            dragNode: null,
            dragging: false,// 交互状态
            // 布局参数
            horizontalSpacing: 200,
            verticalSpacing: 150,
            contextMenu: {
                visible: false,
                x: 0,
                y: 0,
                node: null
            },
            connectionRules: {
                allowLoop: false,
                direction: 'downstream',
                nodeTypes: {
                    'typeA': ['typeB', 'typeC'],
                    'typeB': ['typeC']
                }
            },
        }
    },
    computed: {
        computedHeight() {
            return Math.max(600, this.nodes.length * 50);
        }
    },
    mounted() {
        this.initSampleData();
        window.addEventListener('click', this.closeContextMenu);
    },
    methods: {
        closeContextMenu() {
            this.contextMenu.visible = false;
        },

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

        getLinkColor(status) {
            const colors = {
                completed: '#4CAF50',
                current: '#2196F3',
                pending: '#9E9E9E'
            };
            return colors[status] || '#999';
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

        checkNodeTypes(source, target) {
            const allowedTypes = this.connectionRules.nodeTypes[source.type];
            return allowedTypes ? allowedTypes.includes(target.type) : true;
        },

        showError(message) {
            this.$message({
                message,
                type: 'error',
                duration: 2000
            });
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


        // 上下文菜单
        showContextMenu(node, event) {
            this.contextMenu = {
                visible: true,
                x: event.clientX,
                y: event.clientY,
                node: node
            };
        },

        // 节点交互
        startDrag(node, event) {
            this.dragging = true;
            this.dragNode = node;
            this.startPos = {
                x: event.clientX - node.x,
                y: event.clientY - node.y
            };

            // 固定被拖拽节点
            this.dragNode.fx = this.dragNode.x;
            this.dragNode.fy = this.dragNode.y;
            this.simulation.alphaTarget(0).restart(); // 停止自动布局

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


        saveLayout() {
            // 保存布局逻辑
        },

    }
}
</script>

<style>
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
</style>