// 诊断 asar 头部与内容偏移，并把校验结果写入报告文件
const fs = require('fs');

const ASAR = process.argv[2];
const REPORT = process.argv[3];
const lines = [];
const say = (s) => lines.push(s);

try {
  const fd = fs.openSync(ASAR, 'r');
  const first = Buffer.alloc(64);
  fs.readSync(fd, first, 0, 64, 0);
  say('FIRST_64_HEX=' + first.toString('hex'));
  say('U32@0=' + first.readUInt32LE(0));
  say('U32@4=' + first.readUInt32LE(4));
  say('U32@8=' + first.readUInt32LE(8));
  say('U32@12=' + first.readUInt32LE(12));
  say('FIRST_64_ASCII=' + JSON.stringify(first.toString('utf8').slice(0, 64)));

  // 定位 JSON 起点
  let jsonStart = -1;
  for (let i = 0; i < 64; i++) if (first[i] === 0x7b) { jsonStart = i; break; }
  say('JSON_START=' + jsonStart);

  const lenBuf = Buffer.alloc(4);
  fs.readSync(fd, lenBuf, 0, 4, jsonStart - 4);
  const jsonLen = lenBuf.readUInt32LE(0);
  say('JSON_LEN=' + jsonLen);

  const jsonBuf = Buffer.alloc(jsonLen);
  fs.readSync(fd, jsonBuf, 0, jsonLen, jsonStart);
  const header = JSON.parse(jsonBuf.toString('utf8'));
  say('HEADER_KEYS=' + JSON.stringify(Object.keys(header)));
  say('TOP_LEVEL_FILES=' + JSON.stringify(Object.keys(header.files || {}).slice(0, 10)));

  // 头部结束位置 = jsonStart + jsonLen，再按 4 字节对齐
  const rawEnd = jsonStart + jsonLen;
  const alignedEnd = rawEnd % 4 === 0 ? rawEnd : rawEnd + (4 - (rawEnd % 4));
  say('RAW_END=' + rawEnd + ' ALIGNED_END=' + alignedEnd);
  say('CANDIDATE_OFFSETS: aligned=' + alignedEnd);

  // 取一个已解包文件对照，并列出若干小文件用于字节级校验
  const probes = [];
  (function walk(node, prefix) {
    if (!node || !node.files) return;
    for (const name of Object.keys(node.files)) {
      const child = node.files[name];
      const p = prefix ? prefix + '/' + name : name;
      if (child.files) { walk(child, p); continue; }
      if (child.size === undefined) continue;
      if (child.size > 40 && child.size < 400 && /\.(json|md|txt)$/i.test(p) && !child.unpacked) {
        probes.push({ path: p, size: child.size, offset: Number(child.offset) });
      }
    }
  })(header, '');
  say('PROBE_COUNT=' + probes.length);

  // 用不同基准偏移尝试读取，看哪个能得到合法文本
  for (const base of [alignedEnd, jsonStart + jsonLen, 8 + first.readUInt32LE(0)]) {
    say('--- BASE=' + base + ' ---');
    for (const pr of probes.slice(0, 3)) {
      const buf = Buffer.alloc(Math.min(pr.size, 120));
      fs.readSync(fd, buf, 0, buf.length, base + pr.offset);
      say('  ' + pr.path + ' => ' + JSON.stringify(buf.toString('utf8').slice(0, 100)));
    }
  }
  fs.closeSync(fd);
} catch (e) {
  say('ERROR ' + (e && e.stack ? e.stack : e));
}
fs.writeFileSync(REPORT, lines.join('\n'), 'utf8');
