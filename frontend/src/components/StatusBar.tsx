import type { Status } from '../types'

export function StatusBar({
  status,
  version,
  onShowChangelog,
}: {
  status: Status
  version: string
  onShowChangelog: () => void
}) {
  const dot =
    'status-indicator' +
    (status.kind === 'busy' ? ' active' : status.kind === 'success' ? ' success' : '')
  const text = 'status-text' + (status.kind === 'busy' ? ' busy' : '')

  return (
    <div className="status-bar">
      <div className={dot} />
      <div className={text}>{status.text}</div>
      <div className="status-spacer" />
      <button
        className="status-version"
        title="История обновлений"
        onClick={onShowChangelog}
      >
        v{version}
      </button>
    </div>
  )
}
