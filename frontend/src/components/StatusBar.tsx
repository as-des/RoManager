import type { Status } from '../types'

export function StatusBar({ status }: { status: Status }) {
  const dot =
    'status-indicator' +
    (status.kind === 'busy' ? ' active' : status.kind === 'success' ? ' success' : '')
  const text = 'status-text' + (status.kind === 'busy' ? ' busy' : '')

  return (
    <div className="status-bar">
      <div className={dot} />
      <div className={text}>{status.text}</div>
    </div>
  )
}
