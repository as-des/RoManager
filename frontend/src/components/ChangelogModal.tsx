import { useEffect, useState } from 'react'
import { CHANGELOG } from '../changelog'
import { IconBack } from './icons'

type Lang = 'ru' | 'en'

const TEXT = {
  ru: { title: 'История обновлений', back: 'Назад' },
  en: { title: 'Changelog', back: 'Back' },
} as const

/** Окно истории обновлений. Открывается кнопкой версии в нижней панели. */
export function ChangelogModal({
  open,
  onClose,
}: {
  open: boolean
  onClose: () => void
}) {
  const [lang, setLang] = useState<Lang>('ru')
  const t = TEXT[lang]

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open, onClose])

  return (
    <div
      className={'changelog-overlay' + (open ? ' show' : '')}
      onMouseDown={e => {
        if (e.target === e.currentTarget) onClose()
      }}
    >
      <div className="changelog-modal">
        <div className="changelog-head">
          <div className="changelog-heading">
            <span className="changelog-title">{t.title}</span>
            <span className="changelog-sub">RoManager</span>
          </div>

          <div className="changelog-head-actions">
            <div className="changelog-lang" role="group" aria-label="Language">
              <button
                className={'changelog-lang-btn' + (lang === 'ru' ? ' active' : '')}
                onClick={() => setLang('ru')}
              >
                RU
              </button>
              <button
                className={'changelog-lang-btn' + (lang === 'en' ? ' active' : '')}
                onClick={() => setLang('en')}
              >
                EN
              </button>
            </div>

            <button className="changelog-back" onClick={onClose}>
              <IconBack />
              {t.back}
            </button>
          </div>
        </div>

        <div className="changelog-body">
          {CHANGELOG.map(entry => (
            <section key={entry.version} className="changelog-release">
              <div className="changelog-version-row">
                <span className="changelog-version">v{entry.version}</span>
                <span className="changelog-date">
                  {lang === 'ru' ? entry.date : entry.dateEn}
                </span>
              </div>
              <ul className="changelog-list">
                {(lang === 'ru' ? entry.changes : entry.changesEn).map(change => (
                  <li key={change}>{change}</li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      </div>
    </div>
  )
}
