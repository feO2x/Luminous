import { buildSvgString } from '../logo/geometry.js';

export const EXPORT_SIZES = [16, 32, 64, 128, 256, 512, 1024];
export const NUGET_ICON_SIZE = 128;

function downloadBlob(filename, blob) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

function baseFilename({ variant, background }) {
  const parts = ['luminous-logo'];
  if (variant !== 'color') parts.push(variant);
  if (background !== 'transparent') parts.push(background);
  return parts.join('-');
}

/** Download the logo as an SVG file. */
export function exportSvg(options) {
  const svg = buildSvgString(options);
  downloadBlob(`${baseFilename(options)}.svg`, new Blob([svg], { type: 'image/svg+xml' }));
}

/** Copy the logo SVG markup to the clipboard. */
export function copySvgToClipboard(options) {
  return navigator.clipboard.writeText(buildSvgString(options));
}

/**
 * Download the logo as a PNG rendered at the requested pixel size.
 * The SVG document carries the target size in its width/height attributes so
 * the browser rasterizes the vector artwork at full resolution.
 */
export function exportPng(options) {
  const size = options.size ?? NUGET_ICON_SIZE;
  const svg = buildSvgString({ ...options, size });
  const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }));
  const image = new Image();

  image.onload = () => {
    URL.revokeObjectURL(url);
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const context = canvas.getContext('2d');
    context.drawImage(image, 0, 0, size, size);
    canvas.toBlob((blob) => {
      if (blob) downloadBlob(`${baseFilename(options)}-${size}px.png`, blob);
    }, 'image/png');
  };

  image.onerror = () => URL.revokeObjectURL(url);
  image.src = url;
}
