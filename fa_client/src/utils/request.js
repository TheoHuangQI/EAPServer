import axios from 'axios'

// 创建 axios 实例
const service = axios.create({
  baseURL: 'https://localhost:7255/api/', // 从环境变量读取 API 地址
  timeout: 5000 // 请求超时时间
})

// 请求拦截器
service.interceptors.request.use(
  config => {
    // 在请求发送之前做一些处理
    return config
  },
  error => {
    // 处理请求错误
    return Promise.reject(error)
  }
)

// 响应拦截器
service.interceptors.response.use(
  response => {
    // 对响应数据做一些处理
    return response.data
  },
  error => {
    // 处理响应错误
    return Promise.reject(error)
  }
)

export default service