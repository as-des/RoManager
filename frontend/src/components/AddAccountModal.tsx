import { useEffect, useState } from 'react'
import { rom } from '../bridge'
import { useI18n } from '../i18n'
import { IconGlobe } from './icons'
import { ModalOverlay } from './Modal'

interface Props {
  open: boolean
  onClose: () => void
}

export function AddAccountModal({ open, onClose }: Props) {
  const { t } = useI18n()
  const [cookie, setCookie] = useState('')

  useEffect(() => {
    if (open) setCookie('')
  }, [open])

  return (
    <ModalOverlay open={open}>
      <div className="modal" style={{ maxWidth: 440 }}>
        <h3 style={{ marginBottom: 6 }}>{t.add.title}</h3>
        <p className="hint" style={{ marginBottom: 14 }}>
          {t.add.hint}
        </p>
        <button
          className="btn btn-primary"
          onClick={() => {
            onClose()
            rom()?.OpenBrowserLogin()
          }}
        >
          <IconGlobe />
          {t.add.browser}
        </button>
        <div style={{ height: 10 }} />
        <div className="input-group" style={{ margin: 0 }}>
          <input
            type="password"
            placeholder={t.add.cookie}
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
          {t.add.byCookie}
        </button>
        <button className="btn btn-ghost" style={{ marginTop: 10 }} onClick={onClose}>
          {t.common.cancel}
        </button>
      </div>
    </ModalOverlay>
  )
}
