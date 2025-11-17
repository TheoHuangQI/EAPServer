import request from '@/utils/request'

export function GetList (paramsData) {
    return request({
        url: '/RunHistory',
        method: 'get',
        params: paramsData
      })
  }

export function Delete(data) {
  return request({
    url: '/RunHistory',
    method: 'delete',
    data: data
  })
}