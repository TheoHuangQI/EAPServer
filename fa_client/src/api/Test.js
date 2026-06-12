import request from '@/utils/request'

export function GetTestItemList (paramsData) {
    return request({
        url: '/Test/TestItem',
        method: 'get',
        params: { messageName: paramsData }
      })
  }


export function AddTestItem(data) {
    return request({
      url: '/Test/TestItem',
      method: 'post',
      data: data
    })
  }


export function EditTestItem(data) {
    return request({
      url: '/Test/PutTestItem',
      method: 'put',
      data: data
    })
  }

  
  export function DeleteTestItem(data) {
    return request({
      url: '/Test/TestItem',
      method: 'delete',
      data: data
    })
  }


  export function GetTestBatchList () {
    return request({
        url: '/Test/TestBatch',
        method: 'get'
      })
  }

  export function GetAllBatch () {
    return request({
        url: '/Test/GetAllBatch',
        method: 'get'
      })
  }
  
  export function saveTestBatch(data) {
    return request({
      url: '/Test/saveTestBatch',
      method: 'put',
      data: data
    })
  }
  export function addTestBatch(data) {
    return request({
      url: '/Test/TestBatch',
      method: 'post',
      data: data
    })
  }
  
  export function GetTestTemplateList () {
    return request({
        url: '/Test/TestTemplate',
        method: 'get'
      })
  }

  export function GetAllTemplate () {
    return request({
        url: '/Test/GetAllTemplate',
        method: 'get'
      })
  }
  
  export function saveTestTemplate(data) {
    return request({
      url: '/Test/saveTestTemplate',
      method: 'put',
      data: data
    })
  }
  export function addTestTemplate(data) {
    return request({
      url: '/Test/TestTemplate',
      method: 'post',
      data: data
    })
  }



  export function  GetTestCaseList (paramsData) {
    return request({
        url: '/Test/TestCase/' + paramsData,
        method: 'get'
      })
  }

  export function AddTestCase(data) {
    return request({
      url: '/Test/TestCase',
      method: 'post',
      data: data
    })
  }

  export function EditTestCase(data) {
    return request({
      url: '/Test/TestCase',
      method: 'put',
      data: data
    })
  }


  export function DeleteTestCase(data) {
    return request({
      url: '/Test/TestCase',
      method: 'delete',
      data: data
    })
  }

  export function StartCase(data) {
    return request({
      url: '/Test/StartCase',
      method: 'post',
      data: data
    })
  }