import { useCallback, useEffect, useRef, useState } from 'react'
import { rom } from './bridge'
import { ChangelogModal } from './components/ChangelogModal'
import { NotificationStack } from './components/NotificationStack'
import type { Notice, NoticeAction, NoticeKind } from './components/NotificationStack'
import { ResizeHandles } from './components/ResizeHandles'
import { ShaderBackground } from './components/ShaderBackground'
import { TitleBar } from './components/TitleBar'
import { Dashboard } from './screens/Dashboard'
import { Wizard } from './screens/Wizard'
import type { Screen, Status, StatusKind } from './types'

const IDLE: Status = { text: 'Готов к работе', kind: 'idle' }

export default function App() {
  const [screen, setScreen] = useState<Screen>('landing')
  const [status, setStatusState] = useState<Status>(IDLE)
  const [notices, setNotices] = useState<Notice[]>([])
  const [version, setVersion] = useState('2.1.0')
  const [changelogOpen, setChangelogOpen] = useState(false)

  const statusTimer = useRef<number | null>(null)
  const noticeSeq = useRef(0)
  const noticeTimers = useRef(new Map<number, number>())

  const goTo = useCallback((next: Screen) => setScreen(next), [])

  const dismissNotice = useCallback((id: number) => {
    const timer = noticeTimers.current.get(id)
    if (timer) window.clearTimeout(timer)
    noticeTimers.current.delete(id)
    setNotices(list => list.filter(n => n.id !== id))
  }, [])

  /** success/info уходят сами, danger/warning висят до закрытия. */
  const notify = useCallback(
    (text: string, kind: NoticeKind = 'info', title?: string, actions?: NoticeAction[]) => {
      const id = ++noticeSeq.current
      setNotices(list => [...list.slice(-3), { id, text, kind, title, actions }])
      if (kind === 'success' || kind === 'info') {
        const timer = window.setTimeout(() => dismissNotice(id), 6000)
        noticeTimers.current.set(id, timer)
      }
    },
    [dismissNotice],
  )

  const setStatus = useCallback((text: string, kind: StatusKind = 'idle') => {
    if (statusTimer.current) window.clearTimeout(statusTimer.current)
    setStatusState({ text, kind })
    if (kind === 'success') {
      statusTimer.current = window.setTimeout(() => setStatusState(IDLE), 2500)
    } else if (kind === 'idle' && text !== IDLE.text) {
      statusTimer.current = window.setTimeout(() => setStatusState(IDLE), 3000)
    }
  }, [])

  // Версия для нижней панели.
  useEffect(() => {
    let alive = true
    rom()
      ?.GetVersion()
      .then(v => {
        if (alive && v) setVersion(v)
      })
      .catch(() => {})
    return () => {
      alive = false
    }
  }, [])

  // Контракт с C#: Bridge.Push вызывает window.goTo / window.openModal / __setStatus.
  useEffect(() => {
    window.goTo = id => setScreen(id as Screen)
    window.__enterDashboard = () => setScreen('dashboard')

    // Необработанные ошибки входа раньше были модалкой — теперь это плашка.
    window.openModal = (title, text) => notify(text, 'danger', title)

    // Общий канал уведомлений: window.__notify(text, kind, actions).
    window.__notify = (text, kind, actions) => {
      const k = (kind as NoticeKind) || 'info'
      const mapped: NoticeAction[] = (actions ?? []).map(a => ({
        label: a.label,
        onClick: () => {
          if (a.id === 'settings') rom()?.OpenSettings()
          else if (a.id === 'changelog') setChangelogOpen(true)
        },
      }))
      notify(text, k, undefined, mapped)
    }

    // Всё, что приходит из C# через PushStatus: busy/idle — в нижнюю панель,
    // success/danger — ещё и плашкой сверху.
    window.__setStatus = (text, kind) => {
      const k = (kind as StatusKind) || 'idle'
      setStatus(text, k)
      if (k === 'success') notify(text, 'success')
      else if (k === 'danger') notify(text, 'danger')
    }

    const onKeyDown = (e: KeyboardEvent) => {
      if (document.querySelector('.modal-overlay.show')) {
        if (e.key === 'Escape') setChangelogOpen(false)
        return
      }
      if (e.key === 'Enter') {
        const active = document.querySelector('.screen.active')
        const btn = active?.querySelector<HTMLButtonElement>('.btn-primary:not([disabled])')
        btn?.click()
      }
    }
    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      if (statusTimer.current) window.clearTimeout(statusTimer.current)
      noticeTimers.current.forEach(t => window.clearTimeout(t))
      noticeTimers.current.clear()
      window.goTo = undefined
      window.openModal = undefined
      window.__notify = undefined
      window.__setStatus = undefined
      window.__enterDashboard = undefined
    }
  }, [notify, setStatus])

  return (
    <div className="window-container">
      <ShaderBackground />
      <div className="shader-scrim" />
      <TitleBar />
      <div className="window-content">
        <NotificationStack notices={notices} onDismiss={dismissNotice} />

        {screen === 'dashboard' ? (
          <Dashboard
            status={status}
            setStatus={setStatus}
            version={version}
            onShowChangelog={() => setChangelogOpen(true)}
          />
        ) : (
          <Wizard screen={screen} goTo={goTo} />
        )}

        <ChangelogModal open={changelogOpen} onClose={() => setChangelogOpen(false)} />

        <ResizeHandles />
      </div>
    </div>
  )
}
