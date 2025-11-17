<template>
<div>
    <footer class="footer">
      <p>© 2024 FA测试平台 版权所有</p>
    </footer>
</div>
</template>
<script>
export default {
}
</script>
<style>
.footer {
  background-color: white;
  box-shadow: inset 0 1px 0 rgba(0, 0, 0, 0.05);
  padding: 12px 0;
  text-align: center;
  color: #4b5563;
  font-size: 0.875rem;
  /* 以下代码可确保页脚始终位于底部 */
  margin-top: auto; /* Flexbox 自动将页脚推到底部 */
  position: sticky;
}
</style>