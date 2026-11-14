import { StrictMode, useEffect, useState } from 'react'
import type { ChangeEvent } from 'react'
import { createRoot } from 'react-dom/client'
import { rom } from './bridge'
import { DEFAULT_SHADER_SETTINGS, normalizeShaderSettings, SPEED_MAX, SPEED_MIN } from './shaderSettings'
import type { ShaderSettings } from './shaderSettings'
import './styles/settings.css'

type Tab = 'general' | 'developer' | 'misc'

interface Settings {
  presenceRateSec: number
  autoRecheckMin: number
  autoCookieRefresh: boolean
  joinDelayMs: number
  autoCloseLast: boolean
  multiInstance: boolean
  unlockFps: boolean
  maxFps: number
  sharePresence: boolean
  useBundledChrome: boolean
  devLogMode: boolean
}

const DEFAULTS: Settings = {
  presenceRateSec: 30,
  autoRecheckMin: 15,
  autoCookieRefresh: true,
  joinDelayMs: 2500,
  autoCloseLast: true,
  multiInstance: false,
  unlockFps: false,
  maxFps: 120,
  sharePresence: true,
  useBundledChrome: true,
  devLogMode: false,
}

const clamp = (v: number, min: number, max: number) =>
  Number.isNaN(v) ? min : Math.min(max, Math.max(min, v))

function SettingsWindow() {
  const [tab, setTab] = useState<Tab>('general')
  const [s, setS] = useState<Settings>(DEFAULTS)
  const [shader, setShader] = useState<ShaderSettings>(DEFAULT_SHADER_SETTINGS)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let alive = true
    const bridge = rom()
    if (!bridge) return
    bridge
      .GetSettings()
      .then(json => {
        if (!alive) return
        const loaded = JSON.parse(json) as Partial<Settings>
        setS({ ...DEFAULTS, ...loaded })
      })
      .catch(() => {})
    bridge
      .GetShaderSettings()
      .then(json => {
        if (!alive) return
        try {
          setShader(normalizeShaderSettings(JSON.parse(json)))
        } catch {
          /* значения по умолчанию */
        }
      })
      .catch(() => {})
    return () => {
      alive = false
    }
  }, [])

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') rom()?.CloseSettingsWindow()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [])

  const num = (key: keyof Settings, min: number, max: number) => (e: ChangeEvent<HTMLInputElement>) =>
    setS(prev => ({ ...prev, [key]: clamp(parseInt(e.target.value || '0', 10), min, max) }))

  const bool = (key: keyof Settings) => (e: ChangeEvent<HTMLInputElement>) =>
    setS(prev => ({ ...prev, [key]: e.target.checked }))

  const shaderColor = (key: 'color1' | 'color2' | 'color3') => (e: ChangeEvent<HTMLInputElement>) =>
    setShader(prev => ({ ...prev, [key]: e.target.value }))

  const shaderSpeed = (e: ChangeEvent<HTMLInputElement>) =>
    setShader(prev => ({ ...prev, speed: clamp(parseFloat(e.target.value || '1'), SPEED_MIN, SPEED_MAX) }))

  const save = async () => {
    setSaving(true)
    await rom()?.SaveSettings(JSON.stringify(s))
    rom()?.SaveShaderSettings(JSON.stringify(normalizeShaderSettings(shader)))
    setSaving(false)
    window.setTimeout(() => rom()?.CloseSettingsWindow(), 600)
  }

  return (
    <div className="settings-window">
      <div className="settings-tabs">
        <div
          className={'settings-tab' + (tab === 'general' ? ' active' : '')}
          onClick={() => setTab('general')}
        >
          General
        </div>
        <div
          className={'settings-tab' + (tab === 'developer' ? ' active' : '')}
          onClick={() => setTab('developer')}
        >
          Developer
        </div>
        <div
          className={'settings-tab' + (tab === 'misc' ? ' active' : '')}
          onClick={() => setTab('misc')}
        >
          Miscellaneous
        </div>
      </div>

      <div className="settings-content">
        <div className={'settings-pane' + (tab === 'general' ? ' active' : '')}>
          <div className="sec-title">Сессия и аккаунты</div>

          <div className="setting-item">
            <input
              type="checkbox"
              checked={s.autoCookieRefresh}
              onChange={bool('autoCookieRefresh')}
            />
            <label>Автоматически обновлять cookie</label>
          </div>
          <div className="setting-row-flex">
            <label style={{ opacity: s.autoCookieRefresh ? 1 : 0.5 }}>
              Интервал обновления (мин)
            </label>
            <input
              type="number"
              min={1}
              max={1440}
              value={s.autoRecheckMin}
              disabled={!s.autoCookieRefresh}
              onChange={num('autoRecheckMin', 1, 1440)}
            />
          </div>
          <div className="setting-row-flex">
            <label>Задержка запуска, мс</label>
            <input
              type="number"
              min={0}
              max={30000}
              step={100}
              value={s.joinDelayMs}
              onChange={num('joinDelayMs', 0, 30000)}
            />
          </div>
          <div className="setting-item">
            <input
              type="checkbox"
              checked={s.autoCloseLast}
              onChange={bool('autoCloseLast')}
            />
            <label>Закрывать предыдущий Roblox перед запуском</label>
          </div>

          <div className="sec-title">Multi Roblox</div>
          <div className="setting-item">
            <input
              type="checkbox"
              checked={s.multiInstance}
              onChange={bool('multiInstance')}
            />
            <label>Разрешить несколько клиентов Roblox одновременно</label>
          </div>
          <div className="hint">
            Включайте при закрытом Roblox: приложение удерживает системный мьютекс
            <b> ROBLOX_singletonMutex</b>. Если Roblox уже запущен, настройка применится
            после его закрытия и повторного сохранения.
          </div>

          <div className="hint">
            Некоторые настройки применяются сразу, другие — при следующем запуске.
          </div>
        </div>

        <div className={'settings-pane' + (tab === 'developer' ? ' active' : '')}>
          <div className="sec-title">Отладка</div>
          <div className="setting-item">
            <input
              type="checkbox"
              checked={s.devLogMode}
              onChange={bool('devLogMode')}
            />
            <label>Писать подробный лог (rom.log)</label>
          </div>
          <div className="hint" style={{ fontSize: 11, color: '#999' }}>
            Влияет на мировые исключения и вывода в файл лога.
          </div>
        </div>

        <div className={'settings-pane' + (tab === 'misc' ? ' active' : '')}>
          <div className="sec-title">Presence</div>
          <div className="setting-row-flex">
            <div className="setting-item" style={{ margin: 0 }}>
              <input
                type="checkbox"
                checked={s.sharePresence}
                onChange={bool('sharePresence')}
              />
              <label>Показывать присутствие</label>
            </div>
            <span className="label">Обновление (сек)</span>
            <input
              type="number"
              min={5}
              max={600}
              value={s.presenceRateSec}
              onChange={num('presenceRateSec', 5, 600)}
            />
          </div>
          <div className="sec-title">FPS</div>
          <div className="setting-row-flex">
            <div className="setting-item" style={{ margin: 0 }}>
              <input type="checkbox" checked={s.unlockFps} onChange={bool('unlockFps')} />
              <label>Разблокировать FPS</label>
            </div>
            <span className="label">Макс FPS</span>
            <input
              type="number"
              min={30}
              max={9999}
              value={s.maxFps}
              onChange={num('maxFps', 30, 9999)}
            />
          </div>
          <div className="sec-title">Браузер</div>
          <div className="setting-item">
            <input
              type="checkbox"
              checked={s.useBundledChrome}
              onChange={bool('useBundledChrome')}
            />
            <label>Встраивать Chrome (chrome-win64), если он рядом</label>
          </div>
          <div className="hint">
            Если выключено или папки нет, используется установленный Chrome, затем Edge.
          </div>

          <div className="sec-title">Фон (GLSL)</div>
          <div className="setting-item">
            <input
              type="checkbox"
              checked={shader.enabled}
              onChange={e => setShader(prev => ({ ...prev, enabled: e.target.checked }))}
            />
            <label>Анимированный фон</label>
          </div>
          <div className="setting-row-flex">
            <label>Цвета волн</label>
            <div className="shader-colors">
              <input
                type="color"
                title="Волна 1"
                value={shader.color1}
                onChange={shaderColor('color1')}
              />
              <input
                type="color"
                title="Волна 2"
                value={shader.color2}
                onChange={shaderColor('color2')}
              />
              <input
                type="color"
                title="Волна 3"
                value={shader.color3}
                onChange={shaderColor('color3')}
              />
            </div>
          </div>
          <div className="setting-row-flex">
            <label>Скорость</label>
            <div className="shader-speed">
              <input
                type="range"
                min={SPEED_MIN}
                max={SPEED_MAX}
                step={0.05}
                value={shader.speed}
                onChange={shaderSpeed}
              />
              <span>{shader.speed.toFixed(2)}×</span>
            </div>
          </div>
          <div className="hint">
            Скорость анимации от {SPEED_MIN} до {SPEED_MAX}; 1.00× — как в оригинале. Фон
            применяется сразу после сохранения.
          </div>
        </div>
      </div>

      <div style={{ padding: '16px 20px', background: '#2d2d2d', borderTop: '1px solid #111' }}>
        <button className="btn btn-primary" disabled={saving} onClick={save}>
          Сохранить настройки
        </button>
        <button
          className="btn"
          onClick={() => rom()?.CloseSettingsWindow()}
          style={{ marginLeft: 10 }}
        >
          Отмена
        </button>
      </div>
    </div>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <SettingsWindow />
  </StrictMode>,
)
