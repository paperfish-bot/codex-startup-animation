import { writeFileSync } from 'node:fs';

// Hand-drawn geometry for assets/demo-artwork.svg. This demo avoids tracing
// or redistributing a third-party image. Custom imports use the normal tracer.
const paths = [];
const add = points => paths.push(points.map(([x, y]) => [Math.round(x), Math.round(y)]));

const circle = (cx, cy, radius, steps = 120) => Array.from({ length: steps + 1 }, (_, i) => {
  const angle = i * Math.PI * 2 / steps;
  return [cx + radius * Math.cos(angle), cy + radius * Math.sin(angle)];
});
const cubic = (p0, p1, p2, p3, steps = 72) => Array.from({ length: steps + 1 }, (_, i) => {
  const t = i / steps, u = 1 - t;
  return [0, 1].map(axis => u ** 3 * p0[axis] + 3 * u ** 2 * t * p1[axis] +
    3 * u * t ** 2 * p2[axis] + t ** 3 * p3[axis]);
});

add(circle(1260, 382, 168));
add([[0, 659], [150, 574], [324, 625], [480, 529], [681, 642], [842, 563],
  [1008, 633], [1142, 585], [1310, 647], [1454, 555], [1640, 642], [1780, 571], [1920, 620]]);
add([[0, 716], [190, 647], [368, 702], [550, 625], [724, 721], [902, 650],
  [1097, 711], [1290, 644], [1481, 717], [1650, 653], [1820, 706], [1920, 674]]);
add([...cubic([110, 1035], [466, 770], [525, 481], [697, 445]),
  ...cubic([697, 445], [844, 414], [911, 599], [1032, 574]).slice(1),
  ...cubic([1032, 574], [1146, 550], [1146, 382], [1300, 386]).slice(1)]);
add([...cubic([1568, 18], [1410, 122], [1561, 274], [1708, 305]),
  ...cubic([1708, 305], [1840, 333], [1875, 216], [1967, 201]).slice(1)]);

for (const [base, amplitude, frequency, phase] of [
  [711, 22, 1.9, 0], [731, 25, 2.1, .2], [790, 31, 2.0, 1.1],
  [874, 37, 2.2, .5], [969, 42, 2.0, 1.3]
]) {
  add(Array.from({ length: 121 }, (_, i) => {
    const x = i * 16;
    return [x, base + amplitude * Math.sin((x / 1920) * Math.PI * frequency + phase)];
  }));
}

for (const [x, y, size] of [
  [217, 180, 4], [352, 328, 3], [575, 138, 5], [761, 251, 3],
  [970, 103, 4], [1566, 143, 4], [1724, 399, 3], [1847, 105, 5],
  [143, 429, 3]
]) add(circle(x, y, size, 12));
for (const [x, y, radius] of [[433, 105, 14], [1065, 236, 11],
  [1634, 494, 13], [1742, 226, 9]]) {
  add([[x, y - radius], [x, y + radius]]);
  add([[x - radius, y], [x + radius, y]]);
}

writeFileSync(process.argv[2] || 'assets/contours.js',
  `window.CONTOUR_PATHS=${JSON.stringify(paths)};\n`);
console.log(`Generated ${paths.length} original demo contour paths`);
