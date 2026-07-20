import { useState } from 'react';
import { Logo } from './logo/Logo.jsx';
import { EXPORT_SIZES, NUGET_ICON_SIZE, copySvgToClipboard, exportPng, exportSvg } from './lib/export.js';

const VARIANTS = [
  { id: 'color', label: 'Color' },
  { id: 'mono-black', label: 'Mono black' },
  { id: 'mono-white', label: 'Mono white' },
];

const BACKGROUNDS = [
  { id: 'transparent', label: 'Transparent' },
  { id: 'dark', label: 'Dark' },
  { id: 'white', label: 'White' },
  { id: 'tile', label: 'Icon tile' },
];

const GALLERY = [
  { variant: 'color', background: 'dark', caption: 'Color on dark' },
  { variant: 'color', background: 'white', caption: 'Color on light' },
  { variant: 'mono-black', background: 'white', caption: 'Mono on light' },
  { variant: 'mono-white', background: 'dark', caption: 'Mono on dark' },
  { variant: 'color', background: 'tile', caption: 'App icon tile' },
];

export function App() {
  const [variant, setVariant] = useState('color');
  const [background, setBackground] = useState('transparent');
  const [size, setSize] = useState(NUGET_ICON_SIZE);
  const [copied, setCopied] = useState(false);

  const options = { variant, background, size };

  const handleCopy = async () => {
    await copySvgToClipboard(options);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div className="page">
      <header className="header">
        <h1>Luminous logo</h1>
        <p>Brand mark preview and export. Recommended for NuGet: PNG, {NUGET_ICON_SIZE} × {NUGET_ICON_SIZE} px, transparent background.</p>
      </header>

      <main className="main">
        <section className="preview-card">
          <div className={`preview-stage preview-${background}`}>
            <Logo variant={variant} background={background} size={280} />
            <span className={`wordmark wordmark-${background}`}>Luminous</span>
          </div>
        </section>

        <section className="controls-card">
          <h2>Export</h2>

          <label className="field">
            <span>Variant</span>
            <select value={variant} onChange={(e) => setVariant(e.target.value)}>
              {VARIANTS.map((v) => (
                <option key={v.id} value={v.id}>{v.label}</option>
              ))}
            </select>
          </label>

          <label className="field">
            <span>Background</span>
            <select value={background} onChange={(e) => setBackground(e.target.value)}>
              {BACKGROUNDS.map((b) => (
                <option key={b.id} value={b.id}>{b.label}</option>
              ))}
            </select>
          </label>

          <label className="field">
            <span>Size (PNG)</span>
            <select value={size} onChange={(e) => setSize(Number(e.target.value))}>
              {EXPORT_SIZES.map((s) => (
                <option key={s} value={s}>{s} × {s} px{s === NUGET_ICON_SIZE ? ' (NuGet)' : ''}</option>
              ))}
            </select>
          </label>

          <div className="actions">
            <button type="button" className="primary" onClick={() => exportPng(options)}>
              Download PNG
            </button>
            <button type="button" onClick={() => exportSvg(options)}>
              Download SVG
            </button>
            <button type="button" onClick={handleCopy}>
              {copied ? 'Copied!' : 'Copy SVG'}
            </button>
          </div>

          <button
            type="button"
            className="nuget"
            onClick={() => exportPng({ variant: 'color', background: 'transparent', size: NUGET_ICON_SIZE })}
          >
            Download NuGet icon (PNG {NUGET_ICON_SIZE} × {NUGET_ICON_SIZE})
          </button>
        </section>
      </main>

      <section className="gallery">
        {GALLERY.map((item) => (
          <figure key={item.caption} className={`gallery-item gallery-${item.background}`}>
            <Logo variant={item.variant} background={item.background} size={120} />
            <figcaption>{item.caption}</figcaption>
          </figure>
        ))}
      </section>
    </div>
  );
}
