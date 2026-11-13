import { rom } from '../bridge'

/** Края и углы безрамного окна: тянуть мышью за них = ресайз. */
const EDGES = [
  'top',
  'bottom',
  'left',
  'right',
  'topleft',
  'topright',
  'bottomleft',
  'bottomright',
] as const

export function ResizeHandles() {
  return (
    <>
      {EDGES.map(edge => (
        <div
          key={edge}
          className={'resize-handle ' + edge}
          onPointerDown={e => {
            if (e.button !== 0) return
            e.preventDefault()
            rom()?.BeginResize(edge)
          }}
          onPointerUp={() => rom()?.EndResize()}
          onPointerCancel={() => rom()?.EndResize()}
          onLostPointerCapture={() => rom()?.EndResize()}
        />
      ))}
    </>
  )
}
