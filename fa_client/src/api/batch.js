import request from '@/utils/request'

export function GetBatchList (paramsData) {
    return request({
        url: '/Batch/' + paramsData,
        method: 'get'
      })
  }

export function AddBatch(data) {
  return request({
    url: '/Batch',
    method: 'post',
    data: data
  })
}

export function EditBatch(data) {
  return request({
    url: '/Batch',
    method: 'put',
    data: data
  })
}

export function DeleteBatch(data) {
  return request({
    url: '/Batch',
    method: 'delete',
    data: data
  })
}

export function DeleteBatchs(data) {
  return request({
    url: '/Batch/DeleteBatchs',
    method: 'delete',
    data: data
  })
}
