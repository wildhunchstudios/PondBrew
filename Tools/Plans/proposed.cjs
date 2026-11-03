// GR-34's proposed plan, made from the measured one: every room a quarter larger each way (the whole plan scaled by 1.25
// about the shop's centre), each piece of furniture (a connected patch of the measured obstacles) kept its size and moved
// with its room: along a wall it keeps its distance to that wall, otherwise its centre scales with the room; the doors at
// least 1.4 m; and the second lab room west of the laboratory, with three incubator places and three breeding tank places.
// In: the plans route's folder. Out: <out>/plan.json, <out>/1401-plan-obstacles.png, <out>/1400-plan-view.png (a drawing).
const fs = require('fs'), zlib = require('zlib'), path = require('path');
const [, , src, out] = process.argv;
fs.mkdirSync(out, { recursive: true });
const meta = JSON.parse(fs.readFileSync(path.join(src, 'plan.json'), 'utf8'));
const K = +(process.env.K || 1.25), cell = meta.cell;
eval(fs.readFileSync(path.join(__dirname, 'png.part.js'), 'utf8'));

const img = readPng(path.join(src, '1401-plan-obstacles.png'));
const W = img.w, H = img.h;
const old = new Uint8Array(W * H);
for (let r = 0; r < H; r++) for (let x = 0; x < W; x++) old[(H - 1 - r) * W + x] = img.px[(r * W + x) * img.bpp] > 127 ? 1 : 0;
const ox = x => meta.x0 + (x + 0.5) * cell, oz = z => meta.z0 + (z + 0.5) * cell;

const rooms = [
  { id: 'unit A', r: [-4, -4, 4, 4] }, { id: 'unit B', r: [4, -4, 12, 4] }, { id: 'unit C', r: [2, 4, 12, 9] },
  { id: 'laboratory', r: [-4, 4, 2, 9] }, { id: 'power room', r: [-4, 9, 4, 17] }, { id: 'workshop', r: [4, 9, 12, 15] }, { id: 'research room', r: [-10, 11, -4, 17] },
];
const doors = [[-1.2, -4, 0.2, -4], [4, -1.6, 4, 1.6], [9.9, 4, 11.85, 4], [0.2, 4, 1.4, 4], [0.45, 9, 1.85, 9], [4, 9.6, 4, 11], [-4, 14.65, -4, 16.05]];
const roomAt = (x, z) => rooms.find(m => x >= m.r[0] && x <= m.r[2] && z >= m.r[1] && z <= m.r[3]);
// The walls: cells within 0.15 m of a room's edge (doors are open there). They are drawn afresh, not moved.
const nearWall = (x, z) => rooms.some(m => {
  const [a, b, c, d] = m.r, t = 0.15;
  const onX = (x >= a - t && x <= c + t), onZ = (z >= b - t && z <= d + t);
  return onX && (Math.abs(z - b) < t || Math.abs(z - d) < t) || onZ && (Math.abs(x - a) < t || Math.abs(x - c) < t);
});
// Furniture: connected patches of obstacle cells that are not wall.
const label = new Int32Array(W * H).fill(-1), pieces = [];
for (let i = 0; i < W * H; i++) {
  if (!old[i] || label[i] >= 0 || nearWall(ox(i % W), oz(Math.floor(i / W)))) continue;
  const cells = [], stack = [i]; label[i] = pieces.length;
  while (stack.length) {
    const j = stack.pop(); cells.push(j); const x = j % W, z = (j - x) / W;
    for (const [dx, dz] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const nx = x + dx, nz = z + dz; if (nx < 0 || nz < 0 || nx >= W || nz >= H) continue;
      const k = nz * W + nx; if (old[k] && label[k] < 0 && !nearWall(ox(nx), oz(nz))) { label[k] = pieces.length; stack.push(k); }
    }
  }
  pieces.push(cells);
}
// Where each piece goes: per axis, near a wall (within 0.35 m) it keeps its distance to it; otherwise it scales.
function moved(cells) {
  let x0 = 1e9, x1 = -1e9, z0 = 1e9, z1 = -1e9;
  for (const j of cells) { const x = ox(j % W), z = oz(Math.floor(j / W)); x0 = Math.min(x0, x); x1 = Math.max(x1, x); z0 = Math.min(z0, z); z1 = Math.max(z1, z); }
  const cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, m = roomAt(cx, cz);
  if (!m) return { dx: cx * (K - 1), dz: cz * (K - 1) };
  const [a, b, c, d] = m.r, near = 0.35;
  const dx = x0 - a < near ? a * K - a : c - x1 < near ? c * K - c : cx * (K - 1);
  const dz = z0 - b < near ? b * K - b : d - z1 < near ? d * K - d : cz * (K - 1);
  // Moved on purpose: the workshop's generator, 0.5 m west of the racks along the east wall.
  const shift = Math.hypot(cx - 9.6, cz - 14.35) < 0.6 ? -0.5 : 0;
  return { dx: dx + shift, dz };
}
// The new plan's extent: the old one scaled, and room for the second lab.
const nx0 = -10.5 * K - 0.5, nx1 = 12.5 * K + 0.5, nz0 = -4.5 * K - 0.5, nz1 = 17.5 * K + 0.5;
const NW = Math.round((nx1 - nx0) / cell), NH = Math.round((nz1 - nz0) / cell);
const grid = new Uint8Array(NW * NH);
const put = (x, z) => { const c = Math.floor((x - nx0) / cell), r = Math.floor((z - nz0) / cell); if (c >= 0 && r >= 0 && c < NW && r < NH) grid[r * NW + c] = 1; };
const box = (a, b, c, d) => { for (let z = b; z <= d; z += cell / 2) for (let x = a; x <= c; x += cell / 2) put(x, z); };
for (const cells of pieces) { const { dx, dz } = moved(cells); for (const j of cells) put(ox(j % W) + dx, oz(Math.floor(j / W)) + dz); }
// The new rooms, their walls 0.1 m thick, and the doors (centres scaled, at least 1.4 m wide).
const newRooms = rooms.map(m => ({ id: m.id, r: m.r.map(v => v * K) }));
const lab2 = { id: 'second lab', r: [-10 * K, 4 * K, -4 * K, 11 * K] };
newRooms.push(lab2);
const newDoors = doors.map(([a, b, c, d]) => { const cx = (a + c) / 2 * K, cz = (b + d) / 2 * K, half = Math.max(1.4, Math.hypot(c - a, d - b)) / 2; return a === c ? [cx, cz - half, cx, cz + half] : [cx - half, cz, cx + half, cz]; });
newDoors.push([-4 * K, 10 * K - 0.7, -4 * K, 10 * K + 0.7]); // the second lab's door, from the power room
const inDoor = (x, z) => newDoors.some(([a, b, c, d]) => a === c ? Math.abs(x - a) < 0.2 && z > b && z < d : Math.abs(z - b) < 0.2 && x > a && x < c);
for (const m of newRooms) {
  const [a, b, c, d] = m.r, t = 0.05;
  for (let x = a - t; x <= c + t; x += cell / 2) for (const z of [b, d]) for (let w = -t; w <= t; w += cell / 2) if (!inDoor(x, z + w)) put(x, z + w);
  for (let z = b - t; z <= d + t; z += cell / 2) for (const x of [a, c]) for (let w = -t; w <= t; w += cell / 2) if (!inDoor(x + w, z)) put(x + w, z);
}
// The second lab's places: three incubators along its north wall (0.92 x 0.74, facing south), three breeding tanks along its
// west wall (1.12 x 0.55, facing east), as the kit's pieces measure.
const lab2Pieces = [];
for (const x of [1, 2, 3].map(i => -10 * K + 6 * K * i / 4)) { const p = [x - 0.46, 11 * K - 0.79, x + 0.46, 11 * K - 0.05]; lab2Pieces.push({ kind: 'incubator', r: p }); }
for (const z of [0, 1, 2].map(i => 4 * K + 1.6 + i * 2)) { const p = [-10 * K + 0.05, z - 0.56, -10 * K + 0.6, z + 0.56]; lab2Pieces.push({ kind: 'breeding tank', r: p }); }
for (const p of lab2Pieces) box(...p.r);
// Out: the obstacles (Texture2D order: row 0 at the bottom, so the PNG's first row is the top), the plan's numbers, and a drawing.
const rgb = Buffer.alloc(NW * NH * 3), view = Buffer.alloc(NW * 2 * NH * 2 * 3);
for (let i = 0; i < NW * NH; i++) rgb.fill(grid[i] ? 255 : 0, i * 3, i * 3 + 3);
writePng(path.join(out, '1401-plan-obstacles.png'), NW, NH, rgb, true);
fs.writeFileSync(path.join(out, 'plan.json'), JSON.stringify({ x0: nx0, x1: nx1, z0: nz0, z1: nz1, cell, cellsX: NW, cellsZ: NH, pixelsPerMetre: 40 }));
// The drawing at 40 px a metre: each room's floor in a pale tint of its kind, its pieces in slate.
const roomTint = id => /unit/.test(id) ? [238, 226, 205] : /second/.test(id) ? [214, 236, 230] : [226, 232, 238];
for (let r = 0; r < NH * 2; r++) for (let c = 0; c < NW * 2; c++) {
  const x = nx0 + (c + 0.5) * cell / 2, z = nz0 + (r + 0.5) * cell / 2, g = grid[Math.floor(r / 2) * NW + Math.floor(c / 2)];
  const m = newRooms.find(m => x >= m.r[0] && x <= m.r[2] && z >= m.r[1] && z <= m.r[3]);
  const col = g ? [58, 66, 80] : m ? roomTint(m.id) : [24, 26, 30];
  view.set(col, (r * NW * 2 + c) * 3);
}
writePng(path.join(out, '1400-plan-view.png'), NW * 2, NH * 2, view, true);
fs.writeFileSync(path.join(out, 'rooms.json'), JSON.stringify(newRooms.map(m => {
  const inn = { 'unit A': [-0.5 * K, -3.0 * K], 'unit B': [4.9 * K, 0], 'unit C': [10.9 * K, 4.9 * K], laboratory: [0.8 * K, 4.8 * K], 'power room': [1.15 * K, 9.8 * K], workshop: [4.8 * K, 10.3 * K], 'research room': [-4.8 * K, 15.35 * K], 'second lab': [-4 * K - 0.8, 10 * K] }[m.id];
  return { id: m.id, x0: m.r[0], z0: m.r[1], x1: m.r[2], z1: m.r[3], inX: inn[0], inZ: inn[1] };
})));
fs.writeFileSync(path.join(out, 'lab2.json'), JSON.stringify(lab2Pieces));
console.log(pieces.length + ' pieces moved; ' + NW + ' x ' + NH + ' cells');
