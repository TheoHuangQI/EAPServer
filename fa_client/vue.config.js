const { defineConfig } = require('@vue/cli-service')
module.exports = defineConfig({
  transpileDependencies: true,
  lintOnSave: false,// 关闭 ESLint 检查,
  css: {
    loaderOptions: {
      sass: {
        // 在这里添加额外的配置，例如全局变量等
      }
    }
  },
  configureWebpack: {
    devtool: 'source-map'
  }
})
