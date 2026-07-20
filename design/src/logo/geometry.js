/**
 * Single source of truth for the Luminous mark.
 *
 * The mark is a four-point sparkle built from four curved "blades". The blades
 * do not touch at the center — they swirl around a small sparkle-shaped hole
 * (negative space) whose four tips point at the sparkle's outer tips.
 *
 * The geometry is defined once for the top-right quadrant; the other three
 * blades are 90° rotations of it, which guarantees that the blades tile the
 * sparkle exactly (no gaps or overlaps). Each blade's outer arc and inner arc
 * are mirror-symmetric about the quadrant's diagonal, so the outer silhouette
 * and the inner hole are both perfectly symmetric four-point sparkles.
 * Adjacent blades meet along straight seams that run from each outer tip to
 * the nearest hole tip, so the color boundaries are crisp straight lines.
 *
 * Blade order (clockwise, starting in the top-right quadrant):
 *   0 - cyan       (top-right)
 *   1 - green teal (bottom-right)
 *   2 - royal blue (bottom-left)
 *   3 - blue       (top-left)
 *
 * In the color variant each blade is filled with a linear gradient that runs
 * from its start tip to its end tip, fading from the previous blade's color
 * into the blade's own color. Adjacent blades therefore show exactly the same
 * color where they meet at a tip, so the palette flows around the mark.
 *
 * The gradients also simulate a light source beyond the top-right corner:
 * the colors at the top and right tips are lightened and the colors at the
 * bottom and left tips are darkened (TIP_SHADE), so the mark reads as lit
 * from the top right with the shadow falling toward the bottom left.
 */

export const VIEW_BOX_SIZE = 512;
export const CENTER = VIEW_BOX_SIZE / 2;
export const DARK_BACKGROUND = '#060b24';

// Sparkle tips for the master quadrant (top -> right). The tips are not sharp
// points: each one is truncated by a small flat edge (2 * TIP_HALF_EDGE wide,
// perpendicular to its axis) whose center is where the seam meets the tip.
const TIP_TOP = [256, 64];
const TIP_RIGHT = [448, 256];
const TIP_HALF_EDGE = 9;
const TIP_TOP_CW = [TIP_TOP[0] + TIP_HALF_EDGE, TIP_TOP[1]];

// Outer sparkle edge (top tip -> right tip), concave toward the center.
// OUTER_C2 is the mirror image of OUTER_C1 across the quadrant diagonal
// (x + y = 512), which makes the arc — and thus the silhouette — symmetric.
const OUTER_C1 = [292, 152];

// The central hole is a small four-point sparkle whose tips sit on the axes
// at HOLE_RADIUS from the center. Its edges are deliberately much fuller
// (less concave) than the outer silhouette's, so the hole reads as a solid
// little sparkle instead of four needle-thin spikes.
const HOLE_RADIUS = 72;
const HOLE_TIP_TOP = [256, CENTER - HOLE_RADIUS];

// Seam between adjacent blades, from an outer tip to the nearest hole tip.
// The control points sit on the tip's axis, so the seam is a straight line
// and the fills meet along a crisp, even boundary.
const SEAM_C1 = [256, 104];
const SEAM_C2 = [256, 144];

export const BLADE_COLORS = ['#1fbed2', '#10b981', '#3b5bdb', '#2f8df0'];

const MONO_BLACK = '#0b0c10';
const MONO_WHITE = '#ffffff';

/** Rounded-square app-icon tile settings. */
export const TILE = { radius: 112, inset: 56.32, scale: 0.78 };

/** Rotate a point 90° clockwise around the center, `times` times. */
function rotate([x, y], times = 1) {
  let px = x;
  let py = y;
  for (let i = 0; i < times; i += 1) {
    const nx = VIEW_BOX_SIZE - py;
    const ny = px;
    px = nx;
    py = ny;
  }
  return [px, py];
}

/** Mirror a point across the quadrant diagonal (x + y = 512). */
function reflectDiagonal([x, y]) {
  return [VIEW_BOX_SIZE - y, VIEW_BOX_SIZE - x];
}

const OUTER_C2 = reflectDiagonal(OUTER_C1);
// Counterclockwise end of the right tip's flat edge (mirror of TIP_TOP_CW).
const TIP_RIGHT_CCW = reflectDiagonal(TIP_TOP_CW);

// The hole's top -> right edge (concave toward the center, but much shallower
// than the outer edge). HOLE_C1 trails the hole's top tip and HOLE_C2 mirrors
// it across the quadrant diagonal; both are expressed relative to HOLE_RADIUS
// so the hole keeps its shape when resized.
const HOLE_C1 = [CENTER + 0.36 * HOLE_RADIUS, CENTER - 0.25 * HOLE_RADIUS];
const HOLE_C2 = reflectDiagonal(HOLE_C1);
// Master inner edge (hole tip right -> hole tip top): the hole edge reversed.
const INNER_C1 = HOLE_C2;
const INNER_C2 = HOLE_C1;

function fmt(n) {
  return Number(n.toFixed(2)).toString();
}

function pt([x, y]) {
  return `${fmt(x)},${fmt(y)}`;
}

/** Build the SVG path data for blade `index` (0..3, clockwise from top-right). */
export function bladePath(index) {
  const p0 = rotate(TIP_TOP, index);
  const p0Edge = rotate(TIP_TOP_CW, index);
  const p1Edge = rotate(TIP_RIGHT_CCW, index);
  const p1 = rotate(TIP_RIGHT, index);
  const o1 = rotate(OUTER_C1, index);
  const o2 = rotate(OUTER_C2, index);
  // Seam from p1 to the next hole tip (master seam rotated one extra step).
  const sNext1 = rotate(SEAM_C1, index + 1);
  const sNext2 = rotate(SEAM_C2, index + 1);
  const holeNext = rotate(HOLE_TIP_TOP, index + 1);
  // Inner edge of the hole: from the next hole tip back to this one.
  const i1 = rotate(INNER_C1, index);
  const i2 = rotate(INNER_C2, index);
  const hole = rotate(HOLE_TIP_TOP, index);
  // Seam from the hole tip back to p0 (master seam for this quadrant, reversed).
  const s1 = rotate(SEAM_C1, index);
  const s2 = rotate(SEAM_C2, index);

  return [
    `M ${pt(p0)}`,
    `L ${pt(p0Edge)}`,
    `C ${pt(o1)} ${pt(o2)} ${pt(p1Edge)}`,
    `L ${pt(p1)}`,
    `C ${pt(sNext1)} ${pt(sNext2)} ${pt(holeNext)}`,
    `C ${pt(i1)} ${pt(i2)} ${pt(hole)}`,
    `C ${pt(s2)} ${pt(s1)} ${pt(p0)}`,
    'Z',
  ].join(' ');
}

// Lighting applied to the color shown at each outer tip (top, right, bottom,
// left). Positive values blend toward white, negative toward black. Top and
// right are lit, bottom and left are shadowed: light from the top right.
// The left tip gets a gentler shade because its royal blue base is already
// the darkest color in the palette.
const TIP_SHADE = [0.15, 0.15, -0.22, -0.08];

/** Blend a #rrggbb color toward white (amount > 0) or black (amount < 0). */
function shadeHex(hex, amount) {
  const target = amount >= 0 ? 255 : 0;
  const t = Math.abs(amount);
  const channels = [1, 3, 5].map((i) => {
    const value = parseInt(hex.slice(i, i + 2), 16);
    return Math.round(value + (target - value) * t);
  });
  return `#${channels.map((value) => value.toString(16).padStart(2, '0')).join('')}`;
}

/** The color shown at outer tip `t` (0 top, 1 right, 2 bottom, 3 left). */
function tipColor(t) {
  const tip = t % 4;
  return shadeHex(BLADE_COLORS[(tip + 3) % 4], TIP_SHADE[tip]);
}

/**
 * Linear gradient descriptor for blade `index` in the color variant.
 *
 * The gradient axis runs from the blade's start tip to its end tip (in
 * viewBox coordinates, so use gradientUnits="userSpaceOnUse"). It fades from
 * the previous blade's color into this blade's own color — each shaded by
 * its tip's lighting — which makes the two blades meeting at a tip arrive
 * there with the identical color.
 */
export function bladeGradient(index) {
  const [x1, y1] = rotate(TIP_TOP, index);
  const [x2, y2] = rotate(TIP_TOP, index + 1);
  return {
    id: `luminous-blade-${index}`,
    x1,
    y1,
    x2,
    y2,
    from: tipColor(index),
    to: tipColor(index + 1),
  };
}

export function bladeFill(variant, index) {
  if (variant === 'mono-black') return MONO_BLACK;
  if (variant === 'mono-white') return MONO_WHITE;
  return `url(#${bladeGradient(index).id})`;
}

function backgroundRects(background) {
  switch (background) {
    case 'dark':
      return [`<rect width="${VIEW_BOX_SIZE}" height="${VIEW_BOX_SIZE}" fill="${DARK_BACKGROUND}"/>`];
    case 'white':
      return [`<rect width="${VIEW_BOX_SIZE}" height="${VIEW_BOX_SIZE}" fill="#ffffff"/>`];
    case 'tile': {
      // Rounded-square app-icon tile with the mark inset.
      return {
        rects: [`<rect width="${VIEW_BOX_SIZE}" height="${VIEW_BOX_SIZE}" rx="${TILE.radius}" fill="${DARK_BACKGROUND}"/>`],
        transform: `translate(${TILE.inset} ${TILE.inset}) scale(${TILE.scale})`,
      };
    }
    default:
      return [];
  }
}

/**
 * Build a standalone SVG document for the mark.
 *
 * @param {object} options
 * @param {'color'|'mono-black'|'mono-white'} [options.variant]
 * @param {'transparent'|'dark'|'white'|'tile'} [options.background]
 * @param {number} [options.size] Pixel size used for the width/height attributes.
 * @returns {string} SVG markup.
 */
export function buildSvgString({ variant = 'color', background = 'transparent', size = VIEW_BOX_SIZE } = {}) {
  const bg = backgroundRects(background);
  const rects = Array.isArray(bg) ? bg : bg.rects;
  const transform = Array.isArray(bg) ? null : bg.transform;

  const paths = BLADE_COLORS.map((_, index) => {
    const d = bladePath(index);
    return `    <path d="${d}" fill="${bladeFill(variant, index)}"/>`;
  }).join('\n');

  const defs = variant === 'color'
    ? `  <defs>\n${BLADE_COLORS.map((_, index) => {
      const g = bladeGradient(index);
      return [
        `    <linearGradient id="${g.id}" gradientUnits="userSpaceOnUse" x1="${g.x1}" y1="${g.y1}" x2="${g.x2}" y2="${g.y2}">`,
        `      <stop offset="0" stop-color="${g.from}"/>`,
        `      <stop offset="1" stop-color="${g.to}"/>`,
        `    </linearGradient>`,
      ].join('\n');
    }).join('\n')}\n  </defs>\n`
    : '';

  const body = transform ? `  <g transform="${transform}">\n${paths}\n  </g>` : paths;
  const bgMarkup = rects.length > 0 ? `  ${rects.join('\n  ')}\n` : '';

  return [
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${VIEW_BOX_SIZE} ${VIEW_BOX_SIZE}" width="${size}" height="${size}">`,
    defs + bgMarkup + body,
    `</svg>`,
  ].join('\n');
}
