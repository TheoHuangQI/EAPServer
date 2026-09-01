const fs = require('fs')
const path = require('path')

const files = [
  'node_modules/echarts/lib/processor/dataSample.js',
  'node_modules/echarts/dist/echarts.js',
  'node_modules/echarts/dist/echarts.esm.js',
  'node_modules/echarts/dist/echarts.common.js',
  'node_modules/echarts/dist/echarts.simple.js'
]

const from = "count > 10 && coordSys.type === 'cartesian2d' && sampling"
const to = "count > 10 && coordSys && coordSys.type === 'cartesian2d' && sampling"

for (const file of files) {
  const fullPath = path.join(__dirname, '..', file)

  if (!fs.existsSync(fullPath)) {
    continue
  }

  const content = fs.readFileSync(fullPath, 'utf8')

  if (content.includes(to)) {
    continue
  }

  if (!content.includes(from)) {
    console.warn(`[patch-echarts-datasample] pattern not found: ${file}`)
    continue
  }

  fs.writeFileSync(fullPath, content.replace(from, to))
  console.log(`[patch-echarts-datasample] patched: ${file}`)
}
