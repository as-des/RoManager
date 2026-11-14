import type { CSSProperties } from 'react'
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
  leaving?: boolean
}

function NoticeIcon({ kind }: { kind: NoticeKind }) {
  if (kind === 'success') return <IconCheckCircle />
  if (kind === 'info') return <IconInfoCircle />
  return <IconAlertCircle />
}

/**
 * Плашки над содержимым окна. Новые ложатся поверх старых как карточки
 * (видны только края нижних). Подсветка одна на весь стек.
 */
export function NotificationStack({
  notices,
  onDismiss,
}: {
  notices: Notice[]
  onDismiss: (id: number) => void
}) {
  if (notices.length === 0) return null

  const topKind = notices[0].kind
  const deckDepth = Math.min(notices.length - 1, 3)

  return (
    <div
      className={'notice-stack notice-stack-' + topKind}
      aria-live="polite"
      style={{ paddingBottom: 10 + deckDepth * 8 }}
    >
      {notices.map((n, index) => (
        <div
          key={n.id}
          className={
            'notice notice-' +
            n.kind +
            (index > 0 ? ' is-behind' : '') +
            (n.leaving ? ' is-leaving' : '')
          }
          style={{ '--deck': index } as CSSProperties}
          role="status"
        >
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
