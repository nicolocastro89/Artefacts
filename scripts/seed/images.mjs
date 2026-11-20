// Procedural sample images for seeding, with dependency-free PNG and baseline TIFF encoders.
// The images only need to look plausible as collection photography (an object on a backdrop) and
// to differ per artefact; they are generated from a seed, so every run produces identical files.
import { crc32, deflateSync } from 'node:zlib'

// ---------- encoders ----------

/** 8-bit RGB PNG. */
export function encodePng(width, height, rgb) {
  const raw = Buffer.alloc((width * 3 + 1) * height)
  for (let y = 0; y < height; y++) {
    raw[y * (width * 3 + 1)] = 0 // filter: none
    rgb.copy(raw, y * (width * 3 + 1) + 1, y * width * 3, (y + 1) * width * 3)
  }
  const chunk = (type, data) => {
    const len = Buffer.alloc(4)
    len.writeUInt32BE(data.length)
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data])
    const crc = Buffer.alloc(4)
    crc.writeUInt32BE(crc32(body))
    return Buffer.concat([len, body, crc])
  }
  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(width, 0)
  ihdr.writeUInt32BE(height, 4)
  ihdr.set([8, 2, 0, 0, 0], 8) // 8-bit, truecolour, deflate, adaptive filtering, no interlace
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 6 })),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

/** Baseline uncompressed RGB TIFF (little-endian, one strip, 300 dpi), the way scanners deliver masters. */
export function encodeTiff(width, height, rgb) {
  const entries = [
    [256, 4, 1, width], // ImageWidth (LONG)
    [257, 4, 1, height], // ImageLength
    [258, 3, 3, 'bps'], // BitsPerSample 8,8,8 (offset)
    [259, 3, 1, 1], // Compression: none
    [262, 3, 1, 2], // Photometric: RGB
    [273, 4, 1, 'data'], // StripOffsets
    [277, 3, 1, 3], // SamplesPerPixel
    [278, 4, 1, height], // RowsPerStrip
    [279, 4, 1, rgb.length], // StripByteCounts
    [282, 5, 1, 'xres'], // XResolution (RATIONAL)
    [283, 5, 1, 'yres'], // YResolution
    [284, 3, 1, 1], // PlanarConfiguration: chunky
    [296, 3, 1, 2], // ResolutionUnit: inch
  ]
  const ifdOffset = 8
  const ifdSize = 2 + entries.length * 12 + 4
  const extra = { bps: ifdOffset + ifdSize, xres: ifdOffset + ifdSize + 6, yres: ifdOffset + ifdSize + 14 }
  extra.data = extra.yres + 8
  const header = Buffer.alloc(extra.data)
  header.write('II', 0, 'ascii')
  header.writeUInt16LE(42, 2)
  header.writeUInt32LE(ifdOffset, 4)
  header.writeUInt16LE(entries.length, ifdOffset)
  entries.forEach(([tag, type, count, value], i) => {
    const at = ifdOffset + 2 + i * 12
    header.writeUInt16LE(tag, at)
    header.writeUInt16LE(type, at + 2)
    header.writeUInt32LE(count, at + 4)
    const v = typeof value === 'string' ? extra[value] : value
    if (type === 3 && count === 1) header.writeUInt16LE(v, at + 8)
    else header.writeUInt32LE(v, at + 8)
  })
  header.writeUInt32LE(0, ifdOffset + 2 + entries.length * 12) // no next IFD
  ;[8, 8, 8].forEach((b, i) => header.writeUInt16LE(b, extra.bps + i * 2))
  for (const key of ['xres', 'yres']) {
    header.writeUInt32LE(300, extra[key])
    header.writeUInt32LE(1, extra[key] + 4)
  }
  return Buffer.concat([header, Buffer.from(rgb.buffer, rgb.byteOffset, rgb.length)])
}

// ---------- procedural rendering ----------

/** Small deterministic PRNG (mulberry32). */
export function rng(seed) {
  let a = seed >>> 0
  return () => {
    a = (a + 0x6d2b79f5) >>> 0
    let t = a
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

export function hashSeed(text) {
  let h = 2166136261
  for (const c of text) h = Math.imul(h ^ c.charCodeAt(0), 16777619)
  return h >>> 0
}

const hex = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]
const mix = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t]
const scale = (c, k) => [c[0] * k, c[1] * k, c[2] * k]
const clamp01 = (v) => (v < 0 ? 0 : v > 1 ? 1 : v)
const smooth = (e0, e1, x) => {
  const t = clamp01((x - e0) / (e1 - e0))
  return t * t * (3 - 2 * t)
}

/** Per-pixel grain, so flat areas look photographed rather than drawn. */
function grain(x, y, seed) {
  let h = (x * 374761393 + y * 668265263 + seed * 2147483647) | 0
  h = Math.imul(h ^ (h >>> 13), 1274126177)
  return ((h ^ (h >>> 16)) & 0xff) / 255 - 0.5
}

/** Smooth value noise in [0, 1]. */
function valueNoise(x, y, seed) {
  const xi = Math.floor(x)
  const yi = Math.floor(y)
  const fx = x - xi
  const fy = y - yi
  const r = (i, j) => (grain(i, j, seed) + 0.5)
  const sx = fx * fx * (3 - 2 * fx)
  const sy = fy * fy * (3 - 2 * fy)
  const top = r(xi, yi) + (r(xi + 1, yi) - r(xi, yi)) * sx
  const bottom = r(xi, yi + 1) + (r(xi + 1, yi + 1) - r(xi, yi + 1)) * sx
  return top + (bottom - top) * sy
}

/** A studio backdrop: soft vertical falloff and vignette. */
function backdrop(u, v, base) {
  const fall = 1.06 - 0.22 * v
  const vignette = 1 - 0.35 * ((u - 0.5) ** 2 + (v - 0.45) ** 2)
  return scale(base, fall * vignette)
}

const renderers = {
  /** Framed painting hanging on a wall: gilt frame, landscape or still life inside. */
  painting(u, v, p, x, y) {
    const wall = backdrop(u, v, p.wall)
    const m = 0.08
    const f = 0.05
    if (u < m || u > 1 - m || v < m || v > 1 - m) return wall
    const inner = u > m + f && u < 1 - m - f && v > m + f && v < 1 - m - f
    if (!inner) {
      // Frame: gilt bands with bevel shading.
      const d = Math.min(u - m, 1 - m - u, v - m, 1 - m - v) / f
      const band = 0.75 + 0.25 * Math.sin(d * Math.PI * 3)
      return scale(p.frame, band * (0.8 + 0.3 * (1 - v)))
    }
    const cu = (u - m - f) / (1 - 2 * (m + f))
    const cv = (v - m - f) / (1 - 2 * (m + f))
    const horizon = p.horizon + 0.04 * Math.sin(cu * 7 + p.phase)
    let c = cv < horizon ? mix(p.sky, p.haze, cv / horizon) : mix(p.ground, scale(p.ground, 0.55), (cv - horizon) / (1 - horizon))
    for (const o of p.objects) {
      const d = ((cu - o.x) / o.rx) ** 2 + ((cv - o.y) / o.ry) ** 2
      if (d < 1) c = mix(o.color, scale(o.color, 0.45), Math.sqrt(d) * 0.9 + 0.3 * (cu - o.x) / o.rx)
    }
    const crack = valueNoise(cu * 60, cv * 60, p.seed) > 0.93 ? 0.85 : 1
    return scale(c, crack * (0.92 + 0.12 * valueNoise(cu * 8, cv * 8, p.seed + 1)))
  },

  /** Ceramic vessel on a studio sweep, with glaze shading and painted bands. */
  vessel(u, v, p) {
    let c = backdrop(u, v, p.wall)
    const top = 0.12
    const bottom = 0.86
    // Contact shadow.
    const sd = ((u - 0.5) / 0.3) ** 2 + ((v - bottom - 0.01) / 0.03) ** 2
    if (sd < 1) c = scale(c, 0.65 + 0.35 * sd)
    if (v < top || v > bottom) return c
    const t = (v - top) / (bottom - top)
    const radius = p.neck + (p.belly - p.neck) * Math.sin(Math.PI * Math.min(1, t * p.shape)) ** 1.4 * (1 - 0.35 * t) + (t > 0.94 ? -0.02 : 0)
    const dx = (u - 0.5) / radius
    if (Math.abs(dx) > 1) return c
    const light = 0.35 + 0.75 * Math.sqrt(1 - dx * dx) * (0.75 + 0.25 * -dx)
    let glaze = p.body
    for (const b of p.bands) if (Math.abs(t - b) < 0.035) glaze = p.decoration
    if (p.pattern && t > 0.3 && t < 0.7 && Math.sin(dx * 9 + t * 40) > 0.6) glaze = p.decoration
    const highlight = smooth(0.92, 1, 1 - Math.hypot(dx + 0.45, (t - 0.35) * 3))
    return mix(scale(glaze, light), [255, 255, 250], highlight * 0.8)
  },

  /** Coin or medal on dark velvet: metal disc with rim and relief rings. */
  coin(u, v, p, x, y, w, h) {
    const aspect = w / h
    const du = (u - 0.5) * aspect
    const dv = v - 0.5
    const r = Math.hypot(du, dv) / 0.38
    const velvet = scale(p.wall, 0.35 + 0.15 * valueNoise(u * 40, v * 40, p.seed))
    if (r > 1) return r < 1.06 ? scale(velvet, 0.6) : velvet
    const angle = Math.atan2(dv, du)
    const relief = r > 0.88 ? 1.15 : 0.9 + 0.12 * Math.sin(r * 34 + Math.sin(angle * p.lobes) * 2)
    const light = 0.75 + 0.35 * (-du - dv) / 0.38
    const wear = 0.9 + 0.15 * valueNoise(u * 25, v * 25, p.seed + 3)
    return scale(p.metal, relief * light * wear)
  },

  /** Textile filling the frame: woven checks and stripes with thread texture. */
  textile(u, v, p, x, y) {
    const warp = Math.sin(u * p.freq * Math.PI * 2) > p.duty
    const weft = Math.sin(v * p.freq * 0.7 * Math.PI * 2 + p.phase) > p.duty
    let c = warp && weft ? p.decoration : warp || weft ? mix(p.body, p.decoration, 0.45) : p.body
    if (Math.abs(((v * 6 + p.phase) % 1) - 0.5) < 0.04) c = p.accent
    const thread = 0.85 + 0.3 * ((x + y) % 4 < 2 ? 0.5 : 0) + 0.1 * valueNoise(u * 300, v * 300, p.seed)
    const fold = 0.85 + 0.15 * Math.sin(u * 5 + valueNoise(u * 3, v * 3, p.seed) * 3)
    return scale(c, thread * fold * 0.9)
  },

  /** Drawing or print on aged paper: hatching inside a loose oval, foxing spots. */
  drawing(u, v, p, x, y) {
    let c = mix(p.paper, scale(p.paper, 0.86), valueNoise(u * 6, v * 6, p.seed) * 0.6)
    const m = 0.06
    if (u < m || u > 1 - m || v < m || v > 1 - m) return scale(p.wall, 0.9)
    const d = ((u - 0.5) / 0.3) ** 2 + ((v - 0.48) / 0.34) ** 2 + 0.2 * (valueNoise(u * 4, v * 4, p.seed + 9) - 0.5)
    if (d < 1) {
      const hatch = Math.sin((u + v * p.slant) * p.freq) > 0.55 - d * 0.7
      if (hatch) c = mix(c, p.ink, 0.75 * (1 - d * 0.5))
    }
    if (valueNoise(u * 18, v * 18, p.seed + 5) > 0.88) c = mix(c, [150, 110, 70], 0.35)
    return c
  },
}

const PALETTES = {
  painting: () => ({ wall: '#6b6a66', frame: '#b08a3e', sky: '#8fa3b0', haze: '#d9cfb4', ground: '#5b5a3a' }),
  vessel: () => ({ wall: '#d8d6d1', body: '#f1efe8', decoration: '#2c4a86' }),
  coin: () => ({ wall: '#3a2a4a', metal: '#b89b5e' }),
  textile: () => ({ body: '#8a2d2b', decoration: '#d9b45a', accent: '#1f3a5a' }),
  drawing: () => ({ wall: '#3d3b38', paper: '#e9e0c9', ink: '#3b2a1e' }),
}

/** Renders one sample photograph of an object of the given kind. Colours can be overridden per image. */
export function renderImage(kind, width, height, seedText, colours = {}) {
  const render = renderers[kind]
  if (!render) throw new Error(`Unknown image kind "${kind}"`)
  const seed = hashSeed(seedText)
  const r = rng(seed)
  const named = { ...PALETTES[kind](), ...colours }
  const p = Object.fromEntries(Object.entries(named).map(([k, v]) => [k, hex(v)]))
  Object.assign(p, {
    seed: seed % 10000,
    phase: r() * 6,
    horizon: 0.45 + r() * 0.2,
    objects: Array.from({ length: 2 + Math.floor(r() * 4) }, () => ({
      x: 0.2 + r() * 0.6,
      y: 0.55 + r() * 0.3,
      rx: 0.05 + r() * 0.12,
      ry: 0.04 + r() * 0.1,
      color: [60 + r() * 180, 50 + r() * 150, 20 + r() * 90],
    })),
    neck: 0.06 + r() * 0.05,
    belly: 0.2 + r() * 0.1,
    shape: 0.9 + r() * 0.4,
    bands: [0.1 + r() * 0.1, 0.75 + r() * 0.1],
    pattern: r() > 0.4,
    lobes: 3 + Math.floor(r() * 6),
    freq: kind === 'drawing' ? 160 + r() * 140 : 8 + Math.floor(r() * 14),
    duty: 0.2 + r() * 0.5,
    slant: 0.4 + r() * 1.2,
  })

  const rgb = Buffer.alloc(width * height * 3)
  let i = 0
  for (let y = 0; y < height; y++) {
    const v = y / height
    for (let x = 0; x < width; x++) {
      const c = render(x / width, v, p, x, y, width, height)
      const n = grain(x, y, p.seed) * 10
      rgb[i++] = Math.max(0, Math.min(255, c[0] + n))
      rgb[i++] = Math.max(0, Math.min(255, c[1] + n))
      rgb[i++] = Math.max(0, Math.min(255, c[2] + n))
    }
  }
  return rgb
}
