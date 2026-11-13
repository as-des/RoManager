/** Общие настройки GLSL-фона: используются и в главном окне, и в настройках. */
export interface ShaderSettings {
  /** Включён ли анимированный фон (экономит GPU при выключении). */
  enabled: boolean
  color1: string
  color2: string
  color3: string
  /** Множитель скорости анимации. */
  speed: number
}

/** Значения по умолчанию совпадают с исходным dopdop.html. */
export const DEFAULT_SHADER_SETTINGS: ShaderSettings = {
  enabled: true,
  color1: '#a1d1ff',
  color2: '#c7abff',
  color3: '#7863b3',
  speed: 1,
}

/** Допустимый диапазон скорости — жёстко ограничен ползунком. */
export const SPEED_MIN = 0.5
export const SPEED_MAX = 2

const HEX = /^#[0-9a-f]{6}$/i

const clamp = (v: number, min: number, max: number) =>
  Number.isFinite(v) ? Math.min(max, Math.max(min, v)) : min

export function normalizeShaderSettings(raw: unknown): ShaderSettings {
  const s = (raw ?? {}) as Partial<ShaderSettings> & { speedMin?: number; speedMax?: number }
  const color = (value: unknown, fallback: string) =>
    typeof value === 'string' && HEX.test(value) ? value : fallback

  // Совместимость со старым форматом speedMin/speedMax.
  const legacy = ((s.speedMin ?? 1) + (s.speedMax ?? s.speedMin ?? 1)) / 2
  const speed = clamp(Number(s.speed ?? legacy), SPEED_MIN, SPEED_MAX)

  return {
    enabled: s.enabled !== false,
    color1: color(s.color1, DEFAULT_SHADER_SETTINGS.color1),
    color2: color(s.color2, DEFAULT_SHADER_SETTINGS.color2),
    color3: color(s.color3, DEFAULT_SHADER_SETTINGS.color3),
    speed,
  }
}

export function hexToRgb(hex: string): [number, number, number] {
  const m = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hex)
  if (!m) return [1, 1, 1]
  return [parseInt(m[1], 16) / 255, parseInt(m[2], 16) / 255, parseInt(m[3], 16) / 255]
}
