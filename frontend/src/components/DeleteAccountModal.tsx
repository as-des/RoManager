import { ModalOverlay } from './Modal'

interface Props {
  open: boolean
  text: string
  onConfirm: () => void
  onCancel: () => void
}

export function DeleteAccountModal({ open, text, onConfirm, onCancel }: Props) {
  return (
    <ModalOverlay open={open}>
      <div className="modal">
        <h3>Удалить аккаунты?</h3>
        <p className="hint" style={{ marginBottom: 18 }}>
          {text}
        </p>
        <button className="btn btn-danger" onClick={onConfirm}>
          Удалить
        </button>
        <button className="btn btn-secondary" style={{ marginTop: 10 }} onClick={onCancel}>
          Отмена
        </button>
      </div>
    </ModalOverlay>
  )
}
