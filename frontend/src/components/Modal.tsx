import type { ReactNode } from 'react'

export function ModalOverlay({ open, children }: { open: boolean; children: ReactNode }) {
  return <div className={'modal-overlay' + (open ? ' show' : '')}>{children}</div>
}
