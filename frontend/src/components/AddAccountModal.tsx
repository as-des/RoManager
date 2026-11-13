import { useEffect, useState } from 'react'
import { rom } from '../bridge'
import { IconGlobe } from './icons'
import { ModalOverlay } from './Modal'

interface Props {
  open: boolean
  onClose: () => void
}

export function AddAccountModal({ open, onClose }: Props) {
  const [cookie, setCookie] = useState('')

  useEffect(() => {
    if (open) setCookie('')
  }, [open])

  return (
    <ModalOverlay open={open}>
      <div className="modal" style={{ maxWidth: 440 }}>
        <h3 style={{ marginBottom: 6 }}>Добавить аккаунт</h3>
        <p className="hint" style={{ marginBottom: 14 }}>
          Войти через браузер Roblox или вставить cookie вручную.
        </p>
        <button
          className="btn btn-primary"
          onClick={() => {
            onClose()
            rom()?.OpenBrowserLogin()
          }}
        >
          <IconGlobe />
          Войти через браузер
        </button>
        <div style={{ height: 10 }} />
        <div className="input-group" style={{ margin: 0 }}>
          <input
            type="password"
            placeholder=".ROBLOSECURITY cookie (необязательно)"
            spellCheck={false}
            value={cookie}
            onChange={e => setCookie(e.target.value)}
          />
        </div>
        <div style={{ height: 8 }} />
        <button
          className="btn btn-secondary"
          onClick={() => {
            onClose()
            rom()?.AddAccountManual(cookie)
          }}
        >
          Добавить по cookie
        </button>
        <button className="btn btn-ghost" style={{ marginTop: 10 }} onClick={onClose}>
          Отмена
        </button>
      </div>
    </ModalOverlay>
  )
}
