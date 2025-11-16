<template>
    <div class="dashboard-container">
    </div>
  </template>
  
  <script>
  export default {
    name: 'Dashboard',
  };
  </script>
  
  <style scoped>
  .dashboard-container {
    background-image: url('~@/assets/background.jpg'); /* 设置背景图片 */
    background-position: center; /* 背景图片居中 */
    background-repeat: no-repeat; /* 背景图片不重复 */
    height: 100vh; /* 设置容器高度为视口高度 */
    padding: 20px; /* 添加内边距 */
    color: #fff; /* 设置文字颜色为白色 */
    text-align: center; /* 文字居中 */
  }
  
  h1 {
    font-size: 2.5rem; /* 设置标题字体大小 */
    margin-bottom: 20px; /* 设置标题与段落的间距 */
  }
  
  p {
    font-size: 1.2rem; /* 设置段落字体大小 */
  }
  </style>