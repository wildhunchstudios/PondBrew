// GR-34: walkway widths from the plans route's obstacle map (1401-plan-obstacles.png, 5 cm cells, white = a collider).
// Each free cell's clearance is twice its distance to the nearest obstacle: the widest body that can stand there. A cell is
// on the player's way if a 0.6 m wide body can reach it from the shop door (the player's capsule is 0.3 m in radius).
// Out: clearance.json (per room: the narrowest point on each room's ways, with where) and clearance.png (a heat map).
const fs = require('fs'), zlib = require('zlib'), path = require('path');
const dir = process.argv[2];
const meta = JSON.parse(fs.readFileSync(path.join(dir, 'plan.json'), 'utf8'));

function readPng(file) {
  const b = fs.readFileSync(file);
  let p = 8, w, h, ct, idat = [];
  while (p < b.length) {
    const len = b.readUInt32BE(p), type = b.toString('ascii', p + 4, p + 8), data = b.slice(p + 8, p + 8 + len);
    if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); ct = data[9]; if (data[8] !== 8 || data[12] !== 0) throw new Error('8-bit, not interlaced only'); }
    if (type === 'IDAT') idat.push(data);
    p += 12 + len;
  }
  const bpp = ct === 2 ? 3 : ct === 6 ? 4 : ct === 0 ? 1 : 0; if (!bpp) throw new Error('colour type ' + ct);
  const raw = zlib.inflateSync(Buffer.concat(idat)), stride = w * bpp, out = Buffer.alloc(h * stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], line = raw.slice(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? out[y * stride + x - bpp] : 0, up = y > 0 ? out[(y - 1) * stride + x] : 0, c = x >= bpp && y > 0 ? out[(y - 1) * stride + x - bpp] : 0;
      let v = line[x];
      if (f === 1) v += a; else if (f === 2) v += up; else if (f === 3) v += (a + up) >> 1;
      else if (f === 4) { const pa = Math.abs(up - c), pb = Math.abs(a - c), pc = Math.abs(a + up - 2 * c); v += pa <= pb && pa <= pc ? a : pb <= pc ? up : c; }
      out[y * stride + x] = v & 255;
    }
  }
  return { w, h, bpp, px: out };
}
function writePng(file, w, h, rgb) {
  const crcTable = [...Array(256)].map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
  const crc = buf => { let c = 0xffffffff; for (const x of buf) c = crcTable[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const chunk = (type, data) => { const l = Buffer.alloc(4); l.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type), data]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ih = Buffer.alloc(13); ih.writeUInt32BE(w, 0); ih.writeUInt32BE(h, 4); ih[8] = 8; ih[9] = 2;
  const raw = Buffer.alloc(h * (w * 3 + 1));
  for (let y = 0; y < h; y++) { raw[y * (w * 3 + 1)] = 0; rgb.copy(raw, y * (w * 3 + 1) + 1, (h - 1 - y) * w * 3, (h - y) * w * 3); }
  fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ih), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]));
}

const img = readPng(path.join(dir, '1401-plan-obstacles.png'));
const W = img.w, H = img.h, cell = meta.cell;
// PNG rows run top to bottom; the route wrote row 0 at z0 (Texture2D's first row is its bottom), so PNG row r is z index H-1-r.
const blocked = new Uint8Array(W * H);
for (let r = 0; r < H; r++) for (let x = 0; x < W; x++) blocked[(H - 1 - r) * W + x] = img.px[(r * W + x) * img.bpp] > 127 ? 1 : 0;
// Exact Euclidean distance transform (Felzenszwalb), in cells.
function edt() {
  const INF = 1e12, f = new Float64Array(Math.max(W, H)), d = new Float64Array(W * H);
  const dt1 = (n) => { const v = new Int32Array(n), z = new Float64Array(n + 1), o = new Float64Array(n); let k = 0; v[0] = 0; z[0] = -INF; z[1] = INF;
    for (let q = 1; q < n; q++) { let s; while (true) { s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k]); if (s <= z[k]) k--; else break; } k++; v[k] = q; z[k] = s; z[k + 1] = INF; }
    k = 0; for (let q = 0; q < n; q++) { while (z[k + 1] < q) k++; o[q] = (q - v[k]) * (q - v[k]) + f[v[k]]; } return o; };
  for (let x = 0; x < W; x++) { for (let y = 0; y < H; y++) f[y] = blocked[y * W + x] ? 0 : INF; const o = dt1(H); for (let y = 0; y < H; y++) d[y * W + x] = o[y]; }
  for (let y = 0; y < H; y++) { for (let x = 0; x < W; x++) f[x] = d[y * W + x]; const o = dt1(W); for (let x = 0; x < W; x++) d[y * W + x] = Math.sqrt(o[x]); }
  return d;
}
const dist = edt();
const width = i => 2 * dist[i] * cell; // metres: the widest body standing here
const at = (x, z) => [Math.floor((x - meta.x0) / cell), Math.floor((z - meta.z0) / cell)];
const idx = (cx, cz) => cz * W + cx;
// Each room on its own: from a point just inside its door, the widest body that can walk to each cell of the room (the
// widest path's narrowest point, never leaving the room), and where that narrowest point is. Doors are listed apart.
const heapPush = (heap, i, v) => { heap.push([v, i]); let k = heap.length - 1; while (k > 0) { const p = (k - 1) >> 1; if (heap[p][0] >= heap[k][0]) break; [heap[p], heap[k]] = [heap[k], heap[p]]; k = p; } };
const heapPop = heap => { const top = heap[0], last = heap.pop(); if (heap.length) { heap[0] = last; let k = 0; for (;;) { const l = 2 * k + 1, r = l + 1; let m = k; if (l < heap.length && heap[l][0] > heap[m][0]) m = l; if (r < heap.length && heap[r][0] > heap[m][0]) m = r; if (m === k) break; [heap[m], heap[k]] = [heap[k], heap[m]]; k = m; } } return top; };
const best = new Float64Array(W * H).fill(-1), gapOf = new Int32Array(W * H).fill(-1), inRoom = new Int16Array(W * H).fill(-1);
const rooms = JSON.parse(process.argv[3] || '[]');
const result = { rooms: [] };
rooms.forEach((room, r) => {
  const [x0, z0] = at(room.x0, room.z0), [x1, z1] = at(room.x1 - 1e-6, room.z1 - 1e-6);
  for (let z = z0; z <= z1; z++) for (let x = x0; x <= x1; x++) inRoom[idx(x, z)] = r;
  const heap = [];
  // The seed: the widest free cell within 0.5 m of the way-in point (it starts the way unlimited: the door is apart).
  const [sx, sz] = at(room.inX, room.inZ);
  let seed = -1;
  for (let dz = -10; dz <= 10; dz++) for (let dx = -10; dx <= 10; dx++) { const j = idx(sx + dx, sz + dz); if (inRoom[j] === r && !blocked[j] && (seed < 0 || width(j) > width(seed))) seed = j; }
  if (seed < 0) { result.rooms.push({ id: room.id, error: 'no free cell at the way in' }); return; }
  best[seed] = 99; gapOf[seed] = -1; heapPush(heap, seed, 99);
  while (heap.length) {
    const [v, i] = heapPop(heap); if (v < best[i]) continue;
    const x = i % W, z = (i - x) / W;
    for (const [dx, dz] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const j = idx(x + dx, z + dz); if (inRoom[j] !== r || blocked[j]) continue;
      const wj = width(j), nv = Math.min(v, wj);
      if (nv > best[j]) { best[j] = nv; gapOf[j] = wj < v ? j : gapOf[i]; heapPush(heap, j, nv); }
    }
  }
  const gaps = new Map();
  let open = 0, tight = 0, shut = 0;
  for (let z = z0; z <= z1; z++) for (let x = x0; x <= x1; x++) {
    const i = idx(x, z); if (blocked[i] || width(i) < 0.6) continue;
    open++;
    if (best[i] >= 1.2) continue;
    if (best[i] < 0.6) shut++; else tight++;
    const g = gapOf[i]; if (g < 0) continue;
    const e = gaps.get(g) || { cells: 0 }; e.cells++; gaps.set(g, e);
  }
  const list = [...gaps.entries()].filter(([, e]) => e.cells >= 40).map(([g, e]) => ({ x: +(meta.x0 + (g % W + 0.5) * cell).toFixed(2), z: +(meta.z0 + (Math.floor(g / W) + 0.5) * cell).toFixed(2), width: +width(g).toFixed(2), behind: +(e.cells * cell * cell).toFixed(1) }));
  list.sort((a, b) => a.width - b.width);
  const kept = [];
  for (const p of list) { const k = kept.find(k => Math.hypot(k.x - p.x, k.z - p.z) < 0.6); if (k) k.behind = +(k.behind + p.behind).toFixed(1); else kept.push(p); }
  result.rooms.push({ id: room.id, standing: +(open * cell * cell).toFixed(1), tight: +(tight * cell * cell).toFixed(1), shutOff: +(shut * cell * cell).toFixed(1), gaps: kept });
});
fs.writeFileSync(path.join(dir, 'clearance.json'), JSON.stringify(result, null, 1));
// The heat map: obstacles dark; outside the rooms or too narrow to stand light; where a person could stand, by the widest
// body that gets there from the room's door: red under 0.6 m (the player can't), amber under 1.2 m, green 1.2 m or more.
const rgb = Buffer.alloc(W * H * 3);
for (let i = 0; i < W * H; i++) {
  const c = blocked[i] ? [40, 44, 52] : inRoom[i] < 0 ? [235, 235, 232] : width(i) < 0.6 ? [205, 205, 200] : best[i] < 0.6 ? [220, 60, 50] : best[i] < 1.2 ? [240, 170, 40] : [90, 190, 120];
  rgb[i * 3] = c[0]; rgb[i * 3 + 1] = c[1]; rgb[i * 3 + 2] = c[2];
}
writePng(path.join(dir, 'clearance.png'), W, H, rgb);
console.log(result.rooms.map(r => r.error ? r.id + ': ' + r.error : r.id + ': ' + r.standing + ' m2 to stand on, ' + r.tight + ' m2 only through a gap under 1.2 m, ' + r.shutOff + ' m2 shut off; gaps ' + r.gaps.map(g => g.width + ' m at (' + g.x + ', ' + g.z + ') with ' + g.behind + ' m2 behind').join(', ')).join('\n'));
// The walkways as lines: the ridge of the clearance (cells farther from any obstacle than their neighbours across the
// way), on the rooms' standing space reached from their doors, each coloured by its width: green 1.2 m or more, amber
// 0.6 to 1.2 m, red under 0.6 m (the player's 0.6 m capsule cannot pass). A pinch is a ridge cell narrower than the ridge
// on both sides of it along the way (a saddle), or the narrowest of a narrow stretch.
const ridge = new Uint8Array(W * H);
for (let z = 1; z < H - 1; z++) for (let x = 1; x < W - 1; x++) {
  const i = idx(x, z); if (blocked[i] || inRoom[i] < 0 || width(i) < 0.25) continue;
  const d = dist[i];
  // Both neighbours across no farther from obstacles, and one of them nearer (a strip along a wall has equal neighbours along it).
  const across = (a, b) => dist[a] <= d && dist[b] <= d && (dist[a] < d - 0.3 || dist[b] < d - 0.3);
  if (across(i - 1, i + 1) || across(i - W, i + W) || across(i - W - 1, i + W + 1) || across(i - W + 1, i + W - 1)) ridge[i] = 1;
}
// Keep the ridge where it is a way: it touches standing space (width 0.6 m or more) reached from the door within 1 m on
// two opposite sides, so slots between pieces against a wall and corner tips are left out.
const reachable = i => best[i] >= 0.6 || width(i) >= 0.6 && best[i] >= 0;
const way = new Uint8Array(W * H);
const R = Math.round(1.0 / cell);
for (let i = 0; i < W * H; i++) {
  if (!ridge[i]) continue;
  const x = i % W, z = (i - x) / W;
  const side = (dx, dz) => { for (let k = 1; k <= R; k++) { const j = idx(x + dx * k, z + dz * k); if (j < 0 || j >= W * H || blocked[j]) return false; if (inRoom[j] >= 0 && width(j) >= 0.6 && best[j] >= 0) return true; } return false; };
  if (width(i) >= 0.6 && best[i] >= 0) { way[i] = 1; continue; }
  if ((side(1, 0) && side(-1, 0)) || (side(0, 1) && side(0, -1))) way[i] = 1;
}
// Pinches: along a way, a cell is a pinch if the way has a wider cell on both sides within 0.75 m.
const pinches = [];
const L = Math.round(0.75 / cell);
for (let i = 0; i < W * H; i++) {
  if (!way[i] || width(i) >= 1.2) continue;
  const x = i % W, z = (i - x) / W, w = width(i);
  let wider = 0;
  for (const [dx, dz] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
    for (let k = 1; k <= L; k++) { const j = idx(x + dx * k, z + dz * k); if (blocked[j]) break; if (way[j] && width(j) > w + 0.1) { wider |= (dx !== 0 ? 1 : 2) << (dx + dz > 0 ? 0 : 2); break; } }
  }
  // Wider on both ends of one axis: east and west (bits 0 and 2) or north and south (bits 1 and 3).
  if ((wider & 5) === 5 || (wider & 10) === 10) pinches.push({ i, w, room: inRoom[i] });
}
pinches.sort((a, b) => a.w - b.w);
const keptPinches = [];
for (const p of pinches) {
  const px = meta.x0 + (p.i % W + 0.5) * cell, pz = meta.z0 + (Math.floor(p.i / W) + 0.5) * cell;
  if (!keptPinches.some(k => Math.hypot(k.x - px, k.z - pz) < 0.8)) keptPinches.push({ room: rooms[p.room].id, x: +px.toFixed(2), z: +pz.toFixed(2), width: +p.w.toFixed(2) });
}
result.pinches = keptPinches;
// The overlay on the view picture (40 px a metre against the grid's 20): the ways two pixels wide in their colours, and each
// pinch a ring.
const view = readPng(path.join(dir, '1400-plan-view.png'));
const VW = view.w, VH = view.h, k = Math.round(VW / W);
const out = Buffer.alloc(VW * VH * 3);
for (let r = 0; r < VH; r++) for (let x = 0; x < VW; x++) { const s = (r * VW + x) * view.bpp, o = ((VH - 1 - r) * VW + x) * 3; out[o] = view.px[s] * 0.75; out[o + 1] = view.px[s + 1] * 0.75; out[o + 2] = view.px[s + 2] * 0.75; }
const paint = (vx, vz, c) => { if (vx < 0 || vz < 0 || vx >= VW || vz >= VH) return; const o = (vz * VW + vx) * 3; out[o] = c[0]; out[o + 1] = c[1]; out[o + 2] = c[2]; };
for (let i = 0; i < W * H; i++) {
  if (!way[i]) continue;
  const w = width(i), c = w >= 1.2 ? [70, 220, 120] : w >= 0.6 ? [255, 180, 30] : [255, 50, 40];
  const x = i % W, z = (i - x) / W;
  for (let a = -1; a <= k; a++) for (let b = -1; b <= k; b++) paint(x * k + a, z * k + b, c);
}
for (const p of keptPinches) {
  const cx = (p.x - meta.x0) * (VW / (meta.x1 - meta.x0)), cz = (p.z - meta.z0) * (VH / (meta.z1 - meta.z0));
  const c = p.width >= 0.6 ? [255, 180, 30] : [255, 50, 40];
  for (let t = 0; t < 360; t += 2) for (const rad of [13, 14, 15]) paint(Math.round(cx + rad * Math.cos(t * Math.PI / 180)), Math.round(cz + rad * Math.sin(t * Math.PI / 180)), c);
}
writePng(path.join(dir, 'ways.png'), VW, VH, out);
fs.writeFileSync(path.join(dir, 'clearance.json'), JSON.stringify(result, null, 1));
console.log(keptPinches.map(p => p.room + ': ' + p.width + ' m at (' + p.x + ', ' + p.z + ')').join('\n'));
