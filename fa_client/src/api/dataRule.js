import request from '@/utils/request'

export function GetList (paramsData) {
    return request({
        url: '/DataRule',
        method: 'get',
        params: paramsData
      })
  }

  export function Add(data) {
    return request({
      url: '/DataRule',
      method: 'post',
      data: data
    })
  }
  
  export function Edit(data) {
    return request({
      url: '/DataRule',
      method: 'put',
      data: data
    })
  }

export function Delete(data) {
  return request({
    url: '/DataRule',
    method: 'delete',
    data: data
  })
}