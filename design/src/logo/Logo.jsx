import { useMemo } from 'react';
import {
  BLADE_COLORS,
  DARK_BACKGROUND,
  TILE,
  VIEW_BOX_SIZE,
  bladeFill,
  bladeGradient,
  bladePath,
} from './geometry.js';

/**
 * The Luminous mark as a React component. The geometry is shared with the
 * export pipeline, so what you see here is exactly what gets exported.
 *
 * @param {object} props
 * @param {'color'|'mono-black'|'mono-white'} [props.variant]
 * @param {'transparent'|'dark'|'white'|'tile'} [props.background]
 * @param {number} [props.size]
 */
export function Logo({ variant = 'color', background = 'transparent', size = 256, className }) {
  const paths = useMemo(() => BLADE_COLORS.map((_, index) => bladePath(index)), []);

  const blades = paths.map((d, index) => (
    <path key={index} d={d} fill={bladeFill(variant, index)} />
  ));

  return (
    <svg
      className={className}
      viewBox={`0 0 ${VIEW_BOX_SIZE} ${VIEW_BOX_SIZE}`}
      width={size}
      height={size}
      role="img"
      aria-label="Luminous logo"
    >
      {variant === 'color' && (
        <defs>
          {BLADE_COLORS.map((_, index) => {
            const gradient = bladeGradient(index);
            return (
              <linearGradient
                key={gradient.id}
                id={gradient.id}
                gradientUnits="userSpaceOnUse"
                x1={gradient.x1}
                y1={gradient.y1}
                x2={gradient.x2}
                y2={gradient.y2}
              >
                <stop offset="0" stopColor={gradient.from} />
                <stop offset="1" stopColor={gradient.to} />
              </linearGradient>
            );
          })}
        </defs>
      )}
      {background === 'dark' && (
        <rect width={VIEW_BOX_SIZE} height={VIEW_BOX_SIZE} fill={DARK_BACKGROUND} />
      )}
      {background === 'white' && (
        <rect width={VIEW_BOX_SIZE} height={VIEW_BOX_SIZE} fill="#ffffff" />
      )}
      {background === 'tile' && (
        <rect width={VIEW_BOX_SIZE} height={VIEW_BOX_SIZE} rx={TILE.radius} fill={DARK_BACKGROUND} />
      )}
      {background === 'tile' ? (
        <g transform={`translate(${TILE.inset} ${TILE.inset}) scale(${TILE.scale})`}>{blades}</g>
      ) : (
        blades
      )}
    </svg>
  );
}
