import request from '@/utils/request'

export function Get (paramsData) {
    return request({
        url: '/Carrier',
        method: 'get',
        params: paramsData
      })
  }

export function GetIdle () {
    return request({
        url: '/Carrier/Idle',
        method: 'get'
      })
  }

export function Add(data) {
  return request({
    url: '/Carrier',
    method: 'post',
    data: data
  })
}

export function Edit(data) {
  return request({
    url: '/Carrier',
    method: 'put',
    data: data
  })
}

export function Delete(data) {
  return request({
    url: '/Carrier',
    method: 'delete',
    data: data
  })
}


