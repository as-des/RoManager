import { useEffect, useRef, useState } from 'react'
import { rom } from '../bridge'
import { useI18n } from '../i18n'
import { DEFAULT_SHADER_SETTINGS, normalizeShaderSettings } from '../shaderSettings'
import type { ShaderSettings } from '../shaderSettings'
import { IconGear } from './icons'
import { LanguageSwitch } from './LanguageSwitch'

/**
 * Настройки интерфейса на странице авторизации: язык и анимированный фон.
 * Язык и фон хранятся вне зашифрованного хранилища, поэтому доступны до входа.
 */
export function InterfaceSettings() {
  const { t } = useI18n()
  const [open, setOpen] = useState(false)
  const [shader, setShader] = useState<ShaderSettings>(DEFAULT_SHADER_SETTINGS)
  const rootRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    let alive = true
    rom()
      ?.GetShaderSettings()
      .then(json => {
        if (alive) setShader(normalizeShaderSettings(JSON.parse(json)))
      })
      .catch(() => {})
    return () => {
      alive = false
    }
  }, [])

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false)
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  const toggleShader = (enabled: boolean) => {
    const next = { ...shader, enabled }
    setShader(next)
    window.__applyShaderSettings?.(next)
    rom()?.SaveShaderSettings(JSON.stringify(next))
  }

  return (
    <div className="interface-settings" ref={rootRef}>
      <button
        type="button"
        className={'interface-btn' + (open ? ' active' : '')}
        title={t.wizard.interfaceTitle}
        aria-label={t.wizard.interfaceTitle}
        aria-expanded={open}
        onClick={() => setOpen(v => !v)}
      >
        <IconGear />
      </button>

      {open ? (
        <div className="interface-popover" role="dialog" aria-label={t.wizard.interfaceTitle}>
          <div className="interface-popover-title">{t.wizard.interfaceTitle}</div>

          <div className="interface-row">
            <span>{t.common.language}</span>
            <LanguageSwitch />
          </div>

          <label className="interface-row interface-check">
            <span>{t.wizard.animatedBg}</span>
            <input
              type="checkbox"
              checked={shader.enabled}
              onChange={e => toggleShader(e.target.checked)}
            />
          </label>
        </div>
      ) : null}
    </div>
  )
}
