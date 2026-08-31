<template>
    <div class="container">
        <el-row class="main-wrapper" :gutter="10">
            <!-- 左侧列表 -->
            <el-col :span="4">
                <div class="left-panel">
                    <div class="search-container">
                        <el-input v-model="searchQuery" placeholder="搜索" clearable>
                            <template v-slot:prefix>
<i  class="el-input__icon el-icon-search"></i>
</template>
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
                        <Figure></Figure>
                    </div>
                </div>
            </el-col>
        </el-row>
    </div>
</template>
  
<script>
import Figure from "./Figure.vue";
export default {
    components: {Figure},
    data() {
        return {
            searchQuery: "",
            layoutConfig: {
                type: 'hierarchy', // hierarchy | force
                direction: 'LR', // 布局方向: LR/RL/TB/BT
                spacing: {
                    horizontal: 180,
                    vertical: 120
                },
                forceParams: {
                    charge: -300, // 节点斥力
                    linkDistance: 100, // 连接线长度
                    iterations: 300 // 模拟迭代次数
                }
            },
        };
    },
    methods: {
        // // 更新所有连接路径
        // updateLinksPath() {
        //     this.links = this.links.map(link => {
        //         const source = this.nodes.find(n => n.id === link.sourceId);
        //         const target = this.nodes.find(n => n.id === link.targetId);
        //         return {
        //             ...link,
        //             d: this.generateLinkPath(source, target)
        //         };
        //     });
        // },
        // // 自动布局入口方法
        // autoLayout() {
        //     switch (this.layoutConfig.type) {
        //         case 'hierarchy':
        //             this.runHierarchyLayout();
        //             break;
        //         case 'force':
        //             this.runForceDirectedLayout();
        //             break;
        //     }
        //     this.updateLinksPath();
        // },
        // // 层次布局算法
        // runHierarchyLayout() {
        //     // 1. 构建节点层级关系
        //     const root = this.buildHierarchyTree();
        //     // 2. 选择布局算法
        //     const layout = this.layoutConfig.direction === 'TB' ?
        //         cluster().size([800, 600]) :
        //         tree().nodeSize([this.layoutConfig.spacing.horizontal,
        //             this.layoutConfig.spacing.vertical]);
        //     // 3. 执行布局计算
        //     layout(root);
        //     // 4. 更新节点坐标
        //     root.descendants().forEach((node, i) => {
        //         const vueNode = this.nodes[i];
        //         vueNode.x = node.x;
        //         vueNode.y = node.y;
        //     });
        // },
        // // 构建树形数据结构
        // buildHierarchyTree() {
        //     const nodeMap = new Map(this.nodes.map(n => [n.id, n]));
        //     const rootNodes = this.nodes.filter(n => !this.links.some(l => l.targetId === n.id));
        //     const buildTree = (node) => {
        //         const children = this.links
        //             .filter(l => l.sourceId === node.id)
        //             .map(l => nodeMap.get(l.targetId));
        //         return {
        //             id: node.id,
        //             children: children.map(buildTree)
        //         };
        //     };
        //     return hierarchy(rootNodes.map(buildTree)[0]);
        // },
        // beforeDestroy() {
        //     window.removeEventListener('click', this.closeContextMenu);
        // },
        // // 力导向布局算法
        // runForceDirectedLayout() {
        //     const simulation = forceSimulation(this.nodes)
        //         .force("link", forceLink(this.links))
        //         .force("charge", forceManyBody().strength(this.layoutConfig.forceParams.charge))
        //         .force("x", forceX().strength(0.1))
        //         .force("y", forceY().strength(0.1));
        //     // 配置连接力
        //     simulation.force("link")
        //         .id(d => d.id)
        //         .distance(this.layoutConfig.forceParams.linkDistance)
        //         .links(this.links);
        //     // 运行动画
        //     simulation
        //         .stop()
        //         .tick(this.layoutConfig.forceParams.iterations),
        //         // 手动停止模拟
        //         simulation.stop();
        // },
        // // 布局配置界面
        // layoutControls() {
        //     return (<div class="layout-controls">
        //             <el-select v-model={this.layoutConfig.type}>
        //                 <el-option label="层次布局" value="hierarchy"/>
        //                 <el-option label="力导向布局" value="force"/>
        //             </el-select>

        //             {this.layoutConfig.type === 'hierarchy' && (<el-select v-model={this.layoutConfig.direction}>
        //                     <el-option label="左→右" value="LR"/>
        //                     <el-option label="右→左" value="RL"/>
        //                     <el-option label="上→下" value="TB"/>
        //                     <el-option label="下→上" value="BT"/>
        //                 </el-select>)}

        //             <el-button type="primary" onClick={this.autoLayout}>
        //                 应用布局
        //             </el-button>
        //         </div>);
        // }
    },
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



/* 布局控制样式 */
/* .layout-controls {
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
} */

/* 力导向动画过渡 */
/* .node,
.connection-line {
    transition: all 0.5s ease-out;
} */
</style>