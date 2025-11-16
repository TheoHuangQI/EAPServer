<!-- 代码已包含 CSS：使用 TailwindCSS , 安装 TailwindCSS 后方可看到布局样式效果 -->
<template>
  <div class="app">
    <!-- 顶部导航栏 -->
    <Header></Header>
    <!-- 侧边栏 -->
    <Sidebar></Sidebar>

    <!-- 主内容区 -->
    <main :class="['main', { 'main--expanded': isCollapsed }]">
      <router-view></router-view>
    </main>
    <!-- 底部 -->
    <Footer></Footer>
  </div>
</template>
<script>
import Header from "@/views/layout/Header.vue";
import Sidebar from "@/views/layout/Sidebar.vue";
import Footer from "@/views/layout/Footer.vue";
export default {
  components: {Header,Sidebar,Footer},
  data() {
    return {
    
    };
  },
  mounted() {
    
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

.icon-rotate {
  transform: rotate(90deg);
}
.slide-enter-active,
.slide-leave-active {
  transition: all 0.3s ease;
  max-height: 300px;
}
.slide-enter-from,
.slide-leave-to {
  opacity: 0;
  max-height: 0;
  overflow: hidden;
}
.main {
  flex: 1; /* 占据剩余空间 */
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


.icon-rotate {
  transform: rotate(180deg);
  transition: transform 0.3s;
  margin-left: auto;
}
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s;
}
.fade-enter,
.fade-leave-to {
  opacity: 0;
}
</style>
    