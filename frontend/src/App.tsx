import { useCallback, useEffect, useRef, useState } from 'react'
import { rom } from './bridge'
import { ChangelogModal } from './components/ChangelogModal'
import { NotificationStack } from './components/NotificationStack'
import type { Notice, NoticeAction, NoticeKind } from './components/NotificationStack'
import { ShaderBackground } from './components/ShaderBackground'
import { useI18n } from './i18n'
import { Dashboard } from './screens/Dashboard'
import { Wizard } from './screens/Wizard'
import type { Screen, Status, StatusKind } from './types'

export default function App() {
  const { t } = useI18n()
  const idleText = t.dash.ready
  const [screen, setScreen] = useState<Screen>('landing')
  const [status, setStatusState] = useState<Status>({ text: idleText, kind: 'idle' })
  const [notices, setNotices] = useState<Notice[]>([])
  const [version, setVersion] = useState('2.1.0')
  const [changelogOpen, setChangelogOpen] = useState(false)

  const statusTimer = useRef<number | null>(null)
  const noticeSeq = useRef(0)
  const noticesRef = useRef<Notice[]>([])
  const noticeTimers = useRef(new Map<number, number>())
  const removalTimers = useRef(new Map<number, number>())
  const nativeResizing = useRef(false)
  const resizeIdleTimer = useRef<number | null>(null)

  // Смена языка не должна оставлять старый «Готов к работе» в нижней панели.
  useEffect(() => {
    setStatusState(prev =>
      prev.kind === 'idle' && prev.text !== idleText ? { text: idleText, kind: 'idle' } : prev,
    )
  }, [idleText])

  const goTo = useCallback((next: Screen) => setScreen(next), [])

  const clearNoticeTimer = useCallback((id: number) => {
    const timer = noticeTimers.current.get(id)
    if (timer) window.clearTimeout(timer)
    noticeTimers.current.delete(id)
    const removal = removalTimers.current.get(id)
    if (removal) window.clearTimeout(removal)
    removalTimers.current.delete(id)
  }, [])

  const dismissNotice = useCallback(
    (id: number) => {
      const current = noticesRef.current
      const target = current.find(n => n.id === id)
      if (!target || target.leaving) return
      clearNoticeTimer(id)
      // Сначала проигрываем исчезновение, потом убираем из стека.
      const next = current.map(n => (n.id === id ? { ...n, leaving: true } : n))
      noticesRef.current = next
      setNotices(next)
      const removal = window.setTimeout(() => {
        removalTimers.current.delete(id)
        const rest = noticesRef.current.filter(n => n.id !== id)
        noticesRef.current = rest
        setNotices(rest)
      }, 280)
      removalTimers.current.set(id, removal)
    },
    [clearNoticeTimer],
  )

  /**
   * Одно уведомление на один текст: повторный вызов обновляет существующее,
   * а не добавляет дубликат. У каждого есть время жизни.
   */
  const notify = useCallback(
    (text: string, kind: NoticeKind = 'info', title?: string, actions?: NoticeAction[]) => {
      const existing = noticesRef.current.find(n => n.text.trim() === text.trim())
      const id = existing?.id ?? ++noticeSeq.current
      const item: Notice = {
        id,
        text,
        kind,
        title: title ?? existing?.title,
        actions: actions ?? existing?.actions,
        leaving: false,
      }
      const next = [item, ...noticesRef.current.filter(n => n.id !== id)].slice(0, 4)
      noticesRef.current = next
      setNotices(next)

      clearNoticeTimer(id)
      const lifetime =
        kind === 'success' ? 5000 : kind === 'info' ? 6000 : kind === 'warning' ? 9000 : 12000
      noticeTimers.current.set(
        id,
        window.setTimeout(() => dismissNotice(id), lifetime),
      )
    },
    [clearNoticeTimer, dismissNotice],
  )

  const setStatus = useCallback((text: string, kind: StatusKind = 'idle') => {
    if (statusTimer.current) window.clearTimeout(statusTimer.current)
    setStatusState({ text, kind })
    if (kind === 'success') {
      statusTimer.current = window.setTimeout(
        () => setStatusState({ text: idleText, kind: 'idle' }),
        2500,
      )
    } else if (kind === 'idle' && text !== idleText) {
      statusTimer.current = window.setTimeout(
        () => setStatusState({ text: idleText, kind: 'idle' }),
        3000,
      )
    }
  }, [idleText])

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

    // Нативная рамка: во время перетаскивания C# просит временно упростить отрисовку.
    // Плюс страховка на случай, если событие от C# не пришло: любой resize окна
    // сам включает «лёгкий» режим и выключает его через 180 мс после последнего кадра.
    let resizeOn = false
    const setResizeState = (active: boolean) => {
      if (active === resizeOn) return
      resizeOn = active
      document.documentElement.classList.toggle('is-resizing', active)
      window.dispatchEvent(new CustomEvent('rom:resizing', { detail: active }))
    }

    window.__setResizing = active => {
      nativeResizing.current = !!active
      if (active) {
        if (resizeIdleTimer.current) {
          window.clearTimeout(resizeIdleTimer.current)
          resizeIdleTimer.current = null
        }
        setResizeState(true)
      } else if (!resizeIdleTimer.current) {
        setResizeState(false)
      }
    }

    const onWindowResize = () => {
      setResizeState(true)
      if (resizeIdleTimer.current) window.clearTimeout(resizeIdleTimer.current)
      resizeIdleTimer.current = window.setTimeout(() => {
        resizeIdleTimer.current = null
        if (!nativeResizing.current) setResizeState(false)
      }, 180)
    }
    window.addEventListener('resize', onWindowResize)

    const onKeyDown = (e: KeyboardEvent) => {
      if (document.querySelector('.modal-overlay.show, .changelog-overlay.show')) {
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
      window.removeEventListener('resize', onWindowResize)
      if (resizeIdleTimer.current) window.clearTimeout(resizeIdleTimer.current)
      resizeIdleTimer.current = null
      nativeResizing.current = false
      if (statusTimer.current) window.clearTimeout(statusTimer.current)
      noticeTimers.current.forEach(t => window.clearTimeout(t))
      noticeTimers.current.clear()
      removalTimers.current.forEach(t => window.clearTimeout(t))
      removalTimers.current.clear()
      window.goTo = undefined
      window.openModal = undefined
      window.__notify = undefined
      window.__setStatus = undefined
      window.__enterDashboard = undefined
      window.__setResizing = undefined
      document.documentElement.classList.remove('is-resizing')
    }
  }, [notify, setStatus])

  return (
    <div className="window-container">
      <ShaderBackground />
      <div className="shader-scrim" />
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
      </div>
    </div>
  )
}
