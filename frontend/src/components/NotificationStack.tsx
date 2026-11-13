import { IconAlertCircle, IconCheckCircle, IconClose, IconInfoCircle } from './icons'

export type NoticeKind = 'info' | 'success' | 'danger' | 'warning'

export interface NoticeAction {
  label: string
  onClick: () => void
}

export interface Notice {
  id: number
  title?: string
  text: string
  kind: NoticeKind
  actions?: NoticeAction[]
}

function NoticeIcon({ kind }: { kind: NoticeKind }) {
  if (kind === 'success') return <IconCheckCircle />
  if (kind === 'info') return <IconInfoCircle />
  return <IconAlertCircle />
}

/**
 * Плашки над содержимым окна. Кнопки действий — в ряд,
 * последней всегда идёт крестик.
 */
export function NotificationStack({
  notices,
  onDismiss,
}: {
  notices: Notice[]
  onDismiss: (id: number) => void
}) {
  if (notices.length === 0) return null

  return (
    <div className="notice-stack" aria-live="polite">
      {notices.map(n => (
        <div key={n.id} className={'notice notice-' + n.kind} role="status">
          <div className="notice-icon">
            <NoticeIcon kind={n.kind} />
          </div>
          <div className="notice-copy">
            {n.title ? <div className="notice-title">{n.title}</div> : null}
            <div className="notice-text">{n.text}</div>
          </div>
          {n.actions && n.actions.length > 0 ? (
            <div className="notice-actions">
              {n.actions.map(a => (
                <button key={a.label} className="notice-action" onClick={a.onClick}>
                  {a.label}
                </button>
              ))}
            </div>
          ) : null}
          <button
            className="notice-close"
            title="Закрыть"
            aria-label="Закрыть уведомление"
            onClick={() => onDismiss(n.id)}
          >
            <IconClose />
          </button>
        </div>
      ))}
    </div>
  )
}
