import { useEffect } from 'react'
import { CHANGELOG } from '../changelog'
import { IconClose } from './icons'

/** Окно истории обновлений. Открывается кнопкой версии в нижней панели. */
export function ChangelogModal({
  open,
  onClose,
}: {
  open: boolean
  onClose: () => void
}) {
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
      className={'modal-overlay' + (open ? ' show' : '')}
      onMouseDown={e => {
        if (e.target === e.currentTarget) onClose()
      }}
    >
      <div className="modal changelog-modal">
        <div className="changelog-head">
          <div className="changelog-heading">
            <span className="changelog-title">История обновлений</span>
            <span className="changelog-sub">RoManager</span>
          </div>
          <button className="changelog-close" title="Закрыть" aria-label="Закрыть" onClick={onClose}>
            <IconClose />
          </button>
        </div>

        <div className="changelog-body">
          {CHANGELOG.map(entry => (
            <section key={entry.version} className="changelog-release">
              <div className="changelog-version-row">
                <span className="changelog-version">v{entry.version}</span>
                <span className="changelog-date">{entry.date}</span>
              </div>
              <ul className="changelog-list">
                {entry.changes.map(change => (
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
