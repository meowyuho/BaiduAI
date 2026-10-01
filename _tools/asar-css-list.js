// 解析 Electron asar 归档，列出其中的 CSS 资源并提取（只读，不修改归档）
// 结果写入报告文件，避免依赖 stdout 管道
const fs = require('fs');
const path = require('path');

const ASAR = process.argv[2];
const OUT_DIR = process.argv[3];
const REPORT = process.argv[4];
const lines = [];
const say = (s) => { lines.push(s); };

function readHeader(fd) {
  // Chromium Pickle 结构：[4字节 首层长度][4字节 负载长度][4字节 JSON 长度][JSON...]
  // 文件内容起点 = JSON 起点 + JSON 长度，按 4 字节对齐
  const first = Buffer.alloc(64);
  fs.readSync(fd, first, 0, 64, 0);

  let jsonStart = -1;
  for (let i = 0; i < 64; i++) {
    if (first[i] === 0x7b) { jsonStart = i; break; }
  }
  if (jsonStart < 0) throw new Error('未能定位头部 JSON 起点');

  const lenBuf = Buffer.alloc(4);
  fs.readSync(fd, lenBuf, 0, 4, jsonStart - 4);
  const jsonLen = lenBuf.readUInt32LE(0);

  const jsonBuf = Buffer.alloc(jsonLen);
  fs.readSync(fd, jsonBuf, 0, jsonLen, jsonStart);
  const json = jsonBuf.toString('utf8');
  if (json[0] !== '{') throw new Error('头部 JSON 解析起点不正确');

  const rawEnd = jsonStart + jsonLen;
  const contentOffset = rawEnd % 4 === 0 ? rawEnd : rawEnd + (4 - (rawEnd % 4));
  return { header: JSON.parse(json), contentOffset, jsonStart, jsonLen };
}

try {
  const fd = fs.openSync(ASAR, 'r');
  const { header, contentOffset } = readHeader(fd);
  say('CONTENT_OFFSET=' + contentOffset);

  const files = [];
  (function walk(node, prefix) {
    if (!node || !node.files) return;
    for (const name of Object.keys(node.files)) {
      const child = node.files[name];
      const p = prefix ? prefix + '/' + name : name;
      if (child.files) { walk(child, p); continue; }
      if (child.size === undefined) continue;
      files.push({ path: p, size: child.size, offset: child.offset === undefined ? null : Number(child.offset) });
    }
  })(header, '');

  say('TOTAL_FILES=' + files.length);

  const css = files.filter((f) => f.path.toLowerCase().endsWith('.css'));
  say('CSS_COUNT=' + css.length);
  css.sort((a, b) => b.size - a.size).forEach((f) => say('CSS\t' + f.size + '\t' + f.path));

  const cand = files
    .filter((f) => /theme|token|style|color/i.test(f.path) && f.size > 3000)
    .sort((a, b) => b.size - a.size)
    .slice(0, 40);
  say('--- 主题相关文件 ---');
  cand.forEach((f) => say('CAND\t' + f.size + '\t' + f.path));

  fs.mkdirSync(OUT_DIR, { recursive: true });
  for (const f of css) {
    if (f.offset === null) { say('SKIP_UNPACKED\t' + f.path); continue; }
    const buf = Buffer.alloc(f.size);
    fs.readSync(fd, buf, 0, f.size, contentOffset + f.offset);
    const safe = f.path.replace(/[\\/]/g, '__');
    fs.writeFileSync(path.join(OUT_DIR, safe), buf);
    say('EXTRACTED\t' + safe);
  }
  fs.closeSync(fd);
  say('DONE');
} catch (e) {
  say('ERROR ' + (e && e.stack ? e.stack : e));
}
fs.writeFileSync(REPORT, lines.join('\n'), 'utf8');
