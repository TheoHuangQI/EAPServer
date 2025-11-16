import request from '@/utils/request'

export function login(data) {
  return  request({
    url: '/Login',
    method: 'post',
    data
  })
}
