import { useCallback, useEffect, useState } from 'react'
import { ResizeHandles } from './components/ResizeHandles'
import { ShaderBackground } from './components/ShaderBackground'
import { TitleBar } from './components/TitleBar'
import { Dashboard } from './screens/Dashboard'
import { Wizard } from './screens/Wizard'
import type { Screen } from './types'

interface ErrorModal {
  title: string
  text: string
}

export default function App() {
  const [screen, setScreen] = useState<Screen>('landing')
  const [errorModal, setErrorModal] = useState<ErrorModal | null>(null)

  const goTo = useCallback((next: Screen) => setScreen(next), [])

  // Контракт с C#: Bridge.Push вызывает window.goTo / window.openModal / __enterDashboard.
  useEffect(() => {
    window.goTo = id => setScreen(id as Screen)
    window.openModal = (title, text) => setErrorModal({ title, text })
    window.__enterDashboard = () => setScreen('dashboard')

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Enter') {
        const active = document.querySelector('.screen.active')
        const btn = active?.querySelector<HTMLButtonElement>('.btn-primary:not([disabled])')
        btn?.click()
      }
      if (e.key === 'Escape') setErrorModal(null)
    }
    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      window.goTo = undefined
      window.openModal = undefined
      window.__enterDashboard = undefined
    }
  }, [])

  return (
    <div className="window-container">
      <ShaderBackground />
      <div className="shader-scrim" />
      <TitleBar />
      <div className="window-content">
        {screen === 'dashboard' ? (
          <Dashboard />
        ) : (
          <Wizard screen={screen} goTo={goTo} />
        )}

        <div className={'modal-overlay' + (errorModal ? ' show' : '')}>
          <div className="modal">
            <h3>{errorModal?.title ?? 'Ошибка'}</h3>
            <p className="hint" style={{ marginBottom: 18, whiteSpace: 'pre-line' }}>
              {errorModal?.text ?? ''}
            </p>
            <button className="btn btn-primary" onClick={() => setErrorModal(null)}>
              Понятно
            </button>
          </div>
        </div>

        <ResizeHandles />
      </div>
    </div>
  )
}
