import { useI18n } from '../i18n'
import { ModalOverlay } from './Modal'

interface Props {
  open: boolean
  text: string
  onConfirm: () => void
  onCancel: () => void
}

export function DeleteAccountModal({ open, text, onConfirm, onCancel }: Props) {
  const { t } = useI18n()
  return (
    <ModalOverlay open={open}>
      <div className="modal">
        <h3>{t.dash.deleteHeading}</h3>
        <p className="hint" style={{ marginBottom: 18 }}>
          {text}
        </p>
        <button className="btn btn-danger" onClick={onConfirm}>
          {t.dash.delete}
        </button>
        <button className="btn btn-secondary" style={{ marginTop: 10 }} onClick={onCancel}>
          {t.common.cancel}
        </button>
      </div>
    </ModalOverlay>
  )
}
