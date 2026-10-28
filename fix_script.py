import re
f = r'fa_client\src\views\DataCollection\DataHistory\Index.vue'
with open(f, 'r', encoding='utf-8') as fp:
    content = fp.read()
    
idx = content.find('    getAbnormalText(flag) {\n\n    getAbnormalText')
if idx >= 0:
    content = content[:idx] + content[idx:].replace('    getAbnormalText(flag) {\n\n    getAbnormalText', '    getAbnormalText', 1)

idx2 = content.rfind('    getAbnormalText(flag) {\n      if (!this.chart')
if idx2 >= 0:
    content = content[:idx2] + '    setAllSeriesVisible(visible) {\n      if (!this.chart' + content[idx2 + len('    getAbnormalText'):]

with open(f, 'w', encoding='utf-8') as fp:
    fp.write(content)
    print('Fixed successfully')
