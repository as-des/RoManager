import { useEffect, useRef } from 'react'
import { rom } from '../bridge'
import {
  DEFAULT_SHADER_SETTINGS,
  hexToRgb,
  normalizeShaderSettings,
} from '../shaderSettings'

const VERTEX_SHADER = `
attribute vec2 a_position;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
}
`

const FRAGMENT_SHADER = `
precision mediump float;

uniform vec2 u_resolution;
uniform float u_time;
uniform vec3 u_color1;
uniform vec3 u_color2;
uniform vec3 u_color3;

// --- SIMPLEX NOISE ---
vec3 mod289(vec3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
vec2 mod289(vec2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
vec3 permute(vec3 x) { return mod289(((x*34.0)+1.0)*x); }

float snoise(vec2 v) {
    const vec4 C = vec4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
    vec2 i  = floor(v + dot(v, C.yy));
    vec2 x0 = v -   i + dot(i, C.xx);
    vec2 i1;
    i1 = (x0.x > x0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
    vec4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;
    i = mod289(i);
    vec3 p = permute(permute(i.y + vec3(0.0, i1.y, 1.0)) + i.x + vec3(0.0, i1.x, 1.0));
    vec3 m = max(0.5 - vec3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
    m = m*m;
    m = m*m;
    vec3 x = 2.0 * fract(p * C.www) - 1.0;
    vec3 h = abs(x) - 0.5;
    vec3 ox = floor(x + 0.5);
    vec3 a0 = x - ox;
    m *= 1.79284291400159 - 0.85373472095314 * (a0*a0 + h*h);
    vec3 g;
    g.x  = a0.x  * x0.x  + h.x  * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    return 130.0 * dot(m, g);
}

/* Органическая волна с рябью, морфом и подтёками */
float getOrganicWave(vec2 st, float time,
                     float freq, float speed,
                     float rippleAmp, float morphAmp,
                     float dripStrength,
                     float fadeRate, float thicknessPower) {

    float morph = snoise(vec2(st.x * 1.5, st.y * 0.3 - time * 0.08)) * morphAmp;
    float ripple = snoise(vec2(st.x * 12.0, st.y * 3.0 - time * 0.3)) * rippleAmp;

    float dripMask = snoise(vec2(st.x * 3.0, st.y * 0.8 - time * 0.15));
    dripMask = smoothstep(0.3, 0.9, dripMask);

    float dripStretchX = dripMask * dripStrength * 0.3;
    float dripLiftY = dripMask * dripStrength * 0.5;

    float distortedX = st.x + morph + ripple + dripStretchX;
    float raw_line = sin(distortedX * freq + time * speed + dripLiftY);

    float softness = mix(thicknessPower, thicknessPower * 0.5, dripMask * 0.7);
    float intensity = pow(abs(raw_line), softness);

    float fade = exp(-st.y * fadeRate);
    fade += dripMask * dripStrength * 0.15 * exp(-st.y * fadeRate * 0.5);

    return intensity * clamp(fade, 0.0, 1.0);
}

void main() {
    vec2 st = gl_FragCoord.xy / u_resolution.xy;
    st.x *= u_resolution.x / u_resolution.y;

    vec3 final_color = vec3(0.0);

    // Волна 1
    float wave1 = getOrganicWave(st, u_time, 25.0, 0.4, 0.015, 0.08, 0.6, 5.0, 6.0);
    final_color += u_color1 * wave1 * 0.55;

    // Волна 2
    float wave2 = getOrganicWave(st, u_time, 15.0, 0.6, 0.008, 0.12, 0.4, 8.0, 10.0);
    final_color += u_color2 * wave2 * 0.45;

    // Волна 3
    float wave3 = getOrganicWave(st, u_time, 10.0, 0.25, 0.02, 0.15, 0.3, 12.0, 8.0);
    final_color += u_color3 * wave3 * 0.2;

    // Виньетка
    float dist = distance(st, vec2(0.5, 0.5 * (u_resolution.y/u_resolution.x)));
    float vignette = 1.0 - smoothstep(0.5, 1.0, dist);
    final_color *= mix(0.7, 1.0, vignette);

    gl_FragColor = vec4(final_color, 1.0);
}
`

/** Рендер в 1/3 разрешения — как в исходном dopdop.html. */
const SCALE_FACTOR = 3

function compile(gl: WebGLRenderingContext, type: number, source: string) {
  const shader = gl.createShader(type)
  if (!shader) return null
  gl.shaderSource(shader, source)
  gl.compileShader(shader)
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    console.error('[shader]', gl.getShaderInfoLog(shader))
    gl.deleteShader(shader)
    return null
  }
  return shader
}

export function ShaderBackground() {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const settingsRef = useRef(DEFAULT_SHADER_SETTINGS)
  const enabledRef = useRef(true)

  useEffect(() => {
    settingsRef.current = DEFAULT_SHADER_SETTINGS
    enabledRef.current = DEFAULT_SHADER_SETTINGS.enabled
    let alive = true

    // Живое применение настроек из окна настроек (C# пушит после сохранения).
    const apply = (raw: unknown) => {
      const next = normalizeShaderSettings(raw)
      settingsRef.current = next
      enabledRef.current = next.enabled
      document.documentElement.classList.toggle('shader-off', !next.enabled)
    }
    window.__applyShaderSettings = apply

    // Начальные настройки с диска.
    rom()
      ?.GetShaderSettings()
      .then(json => {
        if (!alive) return
        try {
          apply(JSON.parse(json))
        } catch {
          /* остаются значения по умолчанию */
        }
      })
      .catch(() => {})

    const canvas = canvasRef.current
    if (!canvas) return

    const gl = (canvas.getContext('webgl') ||
      canvas.getContext('experimental-webgl')) as WebGLRenderingContext | null
    if (!gl) return

    const vs = compile(gl, gl.VERTEX_SHADER, VERTEX_SHADER)
    const fs = compile(gl, gl.FRAGMENT_SHADER, FRAGMENT_SHADER)
    if (!vs || !fs) return

    const program = gl.createProgram()
    if (!program) return
    gl.attachShader(program, vs)
    gl.attachShader(program, fs)
    gl.linkProgram(program)
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
      console.error('[shader] link:', gl.getProgramInfoLog(program))
      return
    }

    const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1])
    const buffer = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer)
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW)

    const posLoc = gl.getAttribLocation(program, 'a_position')
    gl.enableVertexAttribArray(posLoc)
    gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0)

    const timeLoc = gl.getUniformLocation(program, 'u_time')
    const resLoc = gl.getUniformLocation(program, 'u_resolution')
    const colorLoc = [
      gl.getUniformLocation(program, 'u_color1'),
      gl.getUniformLocation(program, 'u_color2'),
      gl.getUniformLocation(program, 'u_color3'),
    ]

    let resizeRaf = 0

    const resize = () => {
      resizeRaf = 0
      const realWidth = window.innerWidth
      const realHeight = window.innerHeight
      const renderWidth = Math.floor(realWidth / SCALE_FACTOR)
      const renderHeight = Math.floor(realHeight / SCALE_FACTOR)
      canvas.width = renderWidth
      canvas.height = renderHeight
      canvas.style.width = realWidth + 'px'
      canvas.style.height = realHeight + 'px'
      gl.viewport(0, 0, renderWidth, renderHeight)
    }

    // Ресайз без задержки: один пересчёт на кадр вместо debounce на 100 мс.
    const onResize = () => {
      if (resizeRaf) return
      resizeRaf = requestAnimationFrame(resize)
    }

    // Пока тянется рамка окна, не тратим кадры на анимацию фона.
    let pausedByResize = false
    const onResizing = (e: Event) => {
      pausedByResize = !!(e as CustomEvent).detail
    }

    window.addEventListener('resize', onResize)
    window.addEventListener('rom:resizing', onResizing)
    resize()

    let raf = 0
    let shaderTime = 0
    let lastFrame = performance.now()

    const render = (now: number) => {
      if (!enabledRef.current || pausedByResize) {
        lastFrame = now
        raf = requestAnimationFrame(render)
        return
      }
      const dt = Math.min(0.1, Math.max(0, (now - lastFrame) / 1000))
      lastFrame = now

      const { speed, color1, color2, color3 } = settingsRef.current
      shaderTime += dt * speed

      gl.useProgram(program)
      gl.uniform1f(timeLoc, shaderTime)
      gl.uniform2f(resLoc, canvas.width, canvas.height)
      gl.uniform3fv(colorLoc[0], hexToRgb(color1))
      gl.uniform3fv(colorLoc[1], hexToRgb(color2))
      gl.uniform3fv(colorLoc[2], hexToRgb(color3))
      gl.drawArrays(gl.TRIANGLES, 0, 6)

      raf = requestAnimationFrame(render)
    }
    raf = requestAnimationFrame(render)

    return () => {
      alive = false
      cancelAnimationFrame(raf)
      cancelAnimationFrame(resizeRaf)
      window.removeEventListener('resize', onResize)
      window.removeEventListener('rom:resizing', onResizing)
      if (window.__applyShaderSettings === apply) window.__applyShaderSettings = undefined
      document.documentElement.classList.remove('shader-off')
      gl.deleteBuffer(buffer)
      gl.deleteProgram(program)
      gl.deleteShader(vs)
      gl.deleteShader(fs)
    }
  }, [])

  return <canvas ref={canvasRef} className="shader-bg" aria-hidden="true" />
}
