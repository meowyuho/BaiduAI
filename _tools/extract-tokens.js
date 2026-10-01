// 从已提取的 DSH CSS 中抽取设计变量清单，写入报告文件
const fs = require('fs');
const path = require('path');

const CSS_DIR = process.argv[2];
const REPORT = process.argv[3];

const files = fs.readdirSync(CSS_DIR).filter((f) => f.endsWith('.css'));

// 收集所有变量定义：--name: value
const defs = new Map(); // name -> Set(values)
const perFile = new Map();

for (const f of files) {
  const text = fs.readFileSync(path.join(CSS_DIR, f), 'utf8');
  const re = /(--dsw-[a-zA-Z0-9_-]+)\s*:\s*([^;}]+)/g;
  let m;
  let count = 0;
  while ((m = re.exec(text)) !== null) {
    const name = m[1];
    const value = m[2].trim().replace(/\s+/g, ' ');
    if (!defs.has(name)) defs.set(name, new Map());
    const vm = defs.get(name);
    vm.set(value, (vm.get(value) || 0) + 1);
    count++;
  }
  if (count) perFile.set(f, count);
}

const lines = [];
lines.push('=== 变量定义所在文件 ===');
for (const [f, c] of [...perFile.entries()].sort((a, b) => b[1] - a[1])) lines.push(`${c}\t${f}`);
lines.push('');
lines.push('=== 变量总数: ' + defs.size + ' ===');
lines.push('');

// 按前缀分组输出，便于查找色板
const groups = new Map();
for (const [name, values] of defs) {
  const key = name.split('-').slice(0, 3).join('-');
  if (!groups.has(key)) groups.set(key, []);
  groups.get(key).push([name, values]);
}

for (const [key, items] of [...groups.entries()].sort()) {
  lines.push('########## ' + key + ' ##########');
  for (const [name, values] of items.sort()) {
    const vs = [...values.keys()];
    if (vs.length === 1) {
      lines.push(`${name}: ${vs[0]}`);
    } else {
      lines.push(`${name}:`);
      for (const v of vs) lines.push(`    [${values.get(v)}x] ${v}`);
    }
  }
  lines.push('');
}

fs.writeFileSync(REPORT, lines.join('\n'), 'utf8');
