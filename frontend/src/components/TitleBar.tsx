import { dragWindow, rom } from '../bridge'
import { IconWinClose, IconWinMax, IconWinMin } from './icons'

const logoUrl = `${import.meta.env.BASE_URL}logo.png`

export function TitleBar() {
  return (
    <div
      className="window-titlebar"
      onMouseDown={e => {
        if ((e.target as Element).closest('.win-btn')) return
        dragWindow()
        e.preventDefault()
      }}
    >
      <div className="window-title">
        <img className="app-logo" src={logoUrl} alt="" />
        RoManager
      </div>
      <div className="window-controls">
        <button className="win-btn" onClick={() => rom()?.Minimize()} title="Свернуть">
          <IconWinMin />
        </button>
        <button className="win-btn" onClick={() => rom()?.ToggleMax()} title="Развернуть">
          <IconWinMax />
        </button>
        <button className="win-btn close" onClick={() => rom()?.CloseApp()} title="Закрыть">
          <IconWinClose />
        </button>
      </div>
    </div>
  )
}
