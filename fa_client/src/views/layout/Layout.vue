<!-- 代码已包含 CSS：使用 TailwindCSS , 安装 TailwindCSS 后方可看到布局样式效果 -->
<template>
  <div class="app">
    <!-- 顶部导航栏 -->
    <Header :is-collapsed="isCollapsed" @toggle-collapse="handleToggleCollapse"></Header>
    <!-- 侧边栏 -->
    <Sidebar :is-collapsed="isCollapsed" @toggle-collapse="handleToggleCollapse"></Sidebar>

    <!-- 主内容区 -->
    <main :class="['appMain', { 'main--expanded': isCollapsed }]">
      <router-view v-slot="{ Component }">
        <keep-alive>
          <component :is="Component" :key="routeCacheKey" />
        </keep-alive>
      </router-view>
    </main>
    <!-- 底部 -->
    <!-- <Footer></Footer> -->
  </div>
</template>
<script>
import Header from "@/views/layout/Header.vue";
import Sidebar from "@/views/layout/Sidebar.vue";
import Footer from "@/views/layout/Footer.vue";

const LAYOUT_STATE_KEY = 'fa-layout-state';

function readLayoutState() {
  try {
    return JSON.parse(localStorage.getItem(LAYOUT_STATE_KEY)) || {};
  } catch {
    return {};
  }
}

function saveLayoutState(state) {
  try {
    localStorage.setItem(LAYOUT_STATE_KEY, JSON.stringify(state));
  } catch {
    // Ignore storage errors so layout interactions still work normally.
  }
}

export default {
  components: {Header,Sidebar,Footer},
  data() {
    const layoutState = readLayoutState();

    return {
      isCollapsed: Boolean(layoutState.isCollapsed)
    }
  },
  computed: {
    routeCacheKey() {
      return this.$route.path
    }
  },
  methods: {
    handleToggleCollapse() {
      this.isCollapsed = !this.isCollapsed;
      saveLayoutState({
        isCollapsed: this.isCollapsed
      });
    }
  }
};
</script>
<style scoped>
.app {
  height:100vh;
  display: flex;          /* 启用 Flexbox */
  flex-direction: column; /* 子元素垂直排列 */
  background-color: #f3f4f6;
}
.appMain {
  flex: 1;
  padding-top: 64px;    /* 避开顶部导航栏 */
  margin-left: 256px;   /* 避开侧边栏 */
  transition: margin-left 0.3s;
}
.router-view {
  width: 100%;
  height: 100%;
}
.main--expanded {
  margin-left: 80px;    /* 侧边栏折叠后的宽度 */
}
</style>
