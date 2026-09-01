import request from '@/utils/request'

export function GetFdcSvidDefinitions(paramsData) {
  return request({
    url: '/FdcCollection/svid-definitions',
    method: 'get',
    params: paramsData
  })
}

export function GetLatestFdcValues(paramsData) {
  return request({
    url: '/FdcCollection/latest',
    method: 'get',
    params: paramsData
  })
}

export function CollectFdcValues(data) {
  return request({
    url: '/FdcCollection/collect',
    method: 'post',
    data
  })
}

export function CollectFdcValuesByRules(data) {
  return request({
    url: '/FdcCollection/collect/rules',
    method: 'post',
    data
  })
}
