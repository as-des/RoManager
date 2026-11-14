import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { rom } from '../bridge'
import { IconBack, IconDownload, IconKey, IconUpload } from '../components/icons'
import { InterfaceSettings } from '../components/InterfaceSettings'
import { LanguageSwitch } from '../components/LanguageSwitch'
import { fmt, useI18n } from '../i18n'
import type { Screen } from '../types'

interface Props {
  screen: Exclude<Screen, 'dashboard'>
  goTo: (screen: Screen) => void
}

/** Шапка мастера: «Назад» + точки прогресса (1..4) + язык. */
function WizardShell({
  step,
  back,
  children,
}: {
  step: number
  back: () => void
  children: ReactNode
}) {
  const { t } = useI18n()
  return (
    <div className="screen wizard-layout active">
      <div className="wizard-header">
        <button className="back-btn" onClick={back}>
          <IconBack />
          {t.common.back}
        </button>
        <div className="wizard-head-right">
          <div className="progress">
            {[1, 2, 3, 4].map(i => (
              <div
                key={i}
                className={'progress-dot' + (i < step ? ' done' : i === step ? ' active' : '')}
              />
            ))}
          </div>
          <LanguageSwitch />
        </div>
      </div>
      <div className="card fade-in">{children}</div>
    </div>
  )
}

export function Wizard({ screen, goTo }: Props) {
  const { t } = useI18n()
  const [keyName, setKeyName] = useState('')
  const [keyContent, setKeyContent] = useState<string | null>(null)
  const [dragging, setDragging] = useState(false)

  const [accessPhrase, setAccessPhrase] = useState('')
  const [accessPin, setAccessPin] = useState('')
  const [accessPhraseErr, setAccessPhraseErr] = useState(false)
  const [accessPinErr, setAccessPinErr] = useState(false)

  const [createPhrase, setCreatePhrase] = useState('')
  const [createPin, setCreatePin] = useState('')
  const [confirmPin, setConfirmPin] = useState('')
  const [createPhraseErr, setCreatePhraseErr] = useState(false)
  const [createPinErr, setCreatePinErr] = useState(false)
  const [confirmPinErr, setConfirmPinErr] = useState<'pin' | 'mismatch' | null>(null)

  const fileInput = useRef<HTMLInputElement>(null)

  // нативный drag&drop файла из C# (мимо HTML5 drag событий)
  useEffect(() => {
    window.__keyFileDropped = d => {
      setKeyName(d.name)
      setKeyContent(d.content)
    }
    return () => {
      window.__keyFileDropped = undefined
    }
  }, [])

  const startAccess = () => {
    setKeyName('')
    setKeyContent(null)
    setAccessPhrase('')
    setAccessPin('')
    setAccessPhraseErr(false)
    setAccessPinErr(false)
    if (fileInput.current) fileInput.current.value = ''
    goTo('access-file')
  }

  const startCreate = () => {
    setCreatePhrase('')
    setCreatePin('')
    setConfirmPin('')
    setCreatePhraseErr(false)
    setCreatePinErr(false)
    setConfirmPinErr(null)
    goTo('create-phrase')
  }

  const readFile = async (file: File) => {
    setKeyName(file.name)
    try {
      setKeyContent(await file.text())
    } catch {
      setKeyContent(null)
    }
  }

  const validatePhrase = (
    value: string,
    setError: (v: boolean) => void,
    next: Screen,
  ) => {
    if (value.trim().split(/\s+/).filter(Boolean).length !== 2) {
      setError(true)
      return
    }
    setError(false)
    goTo(next)
  }

  const validatePin = (value: string, setError: (v: boolean) => void, next: Screen) => {
    if (!/^\d{4,6}$/.test(value.trim())) {
      setError(true)
      return
    }
    setError(false)
    goTo(next)
  }

  const confirmCreatePin = () => {
    if (!createPin) {
      goTo('create-pin')
      return
    }
    if (!/^\d{4,6}$/.test(confirmPin)) {
      setConfirmPinErr('pin')
      return
    }
    if (createPin !== confirmPin) {
      setConfirmPinErr('mismatch')
      return
    }
    setConfirmPinErr(null)
    goTo('create-done')
    rom()?.CreateKeyFile(createPhrase, createPin, confirmPin)
  }

  const maskedPhrase = accessPhrase.trim()
    ? accessPhrase
        .trim()
        .split(/\s+/)
        .map(w => '•'.repeat(Math.min(w.length, 6)))
        .join(' ')
    : '•••• ••••'

  const pinInput = (
    value: string,
    onChange: (v: string) => void,
    maxLength = 6,
  ) => (
    <input
      type="password"
      maxLength={maxLength}
      inputMode="numeric"
      autoComplete="off"
      value={value}
      onChange={e => onChange(e.target.value.replace(/\D/g, '').slice(0, maxLength))}
    />
  )

  const dots = (count: number) => (
    <div className="pin-dots">
      {[0, 1, 2, 3, 4, 5].map(i => (
        <div key={i} className={'pin-dot' + (i < count ? ' filled' : '')} />
      ))}
    </div>
  )

  if (screen === 'landing') {
    return (
      <div className="screen active wizard-layout">
        <div className="auth-topbar">
          <LanguageSwitch />
          <InterfaceSettings />
        </div>
        <div className="landing fade-in" style={{ paddingTop: 40 }}>
          <button className="btn btn-primary" onClick={startAccess}>
            {t.wizard.haveAccess}
          </button>
          <button className="btn btn-secondary" style={{ marginTop: 10 }} onClick={startCreate}>
            {t.wizard.noAccess}
          </button>
        </div>
      </div>
    )
  }

  if (screen === 'access-file') {
    return (
      <WizardShell step={1} back={() => goTo('landing')}>
        <div className="step-label">{fmt(t.wizard.step, { n: 1 })}</div>
        <h2>{t.wizard.keyFile}</h2>
        <div
          className={'drop-zone' + (dragging ? ' dragover' : '')}
          onClick={() => fileInput.current?.click()}
          onDragOver={e => {
            e.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={e => {
            e.preventDefault()
            setDragging(false)
            if (e.dataTransfer.files.length) void readFile(e.dataTransfer.files[0])
          }}
        >
          <div className="icon-wrap">
            <IconUpload />
          </div>
          <p>{t.wizard.dropHint}</p>
          <div className="filename">{keyName}</div>
        </div>
        <input
          ref={fileInput}
          type="file"
          accept=".json"
          style={{ display: 'none' }}
          onChange={e => {
            if (e.target.files?.length) void readFile(e.target.files[0])
          }}
        />
        <button
          className="btn btn-primary"
          disabled={!keyContent}
          onClick={() => goTo('access-phrase')}
        >
          {t.common.next}
        </button>
      </WizardShell>
    )
  }

  if (screen === 'access-phrase') {
    return (
      <WizardShell step={2} back={() => goTo('access-file')}>
        <div className="step-label">{fmt(t.wizard.step, { n: 2 })}</div>
        <h2>{t.wizard.keyPhrase}</h2>
        <p className="hint">{t.wizard.twoWords}</p>
        <div className="input-group">
          <input
            type="text"
            autoComplete="off"
            spellCheck={false}
            value={accessPhrase}
            onChange={e => {
              setAccessPhrase(e.target.value)
              setAccessPhraseErr(false)
            }}
          />
          <div className={'error' + (accessPhraseErr ? ' show' : '')}>
            {t.wizard.phraseError}
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => validatePhrase(accessPhrase, setAccessPhraseErr, 'access-pin')}
        >
          {t.common.next}
        </button>
      </WizardShell>
    )
  }

  if (screen === 'access-pin') {
    return (
      <WizardShell step={3} back={() => goTo('access-phrase')}>
        <div className="step-label">{fmt(t.wizard.step, { n: 3 })}</div>
        <h2>{t.wizard.pin}</h2>
        {dots(accessPin.length)}
        <p className="hint">{t.wizard.pinHint}</p>
        <div className="input-group">
          {pinInput(accessPin, v => {
            setAccessPin(v)
            setAccessPinErr(false)
          })}
          <div className={'error' + (accessPinErr ? ' show' : '')}>
            {t.wizard.pinError}
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => validatePin(accessPin, setAccessPinErr, 'access-confirm')}
        >
          {t.common.next}
        </button>
      </WizardShell>
    )
  }

  if (screen === 'access-confirm') {
    return (
      <WizardShell step={4} back={() => goTo('access-pin')}>
        <div className="step-label">{fmt(t.wizard.step, { n: 4 })}</div>
        <h2>{t.wizard.confirm}</h2>
        <div className="summary">
          <div className="summary-row">
            <span className="label">{t.wizard.file}</span>
            <span className="value">{keyName || t.wizard.keyJson}</span>
          </div>
          <div className="summary-row">
            <span className="label">{t.wizard.phrase}</span>
            <span className="value">{maskedPhrase}</span>
          </div>
          <div className="summary-row">
            <span className="label">PIN</span>
            <span className="value">{'•'.repeat(accessPin.length || 4)}</span>
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => rom()?.CompleteAccess(keyContent, accessPhrase, accessPin)}
        >
          {t.wizard.confirmLogin}
        </button>
      </WizardShell>
    )
  }

  if (screen === 'create-phrase') {
    return (
      <WizardShell step={1} back={() => goTo('landing')}>
        <div className="step-label">{fmt(t.wizard.step, { n: 1 })}</div>
        <h2>{t.wizard.createPhrase}</h2>
        <p className="hint">{t.wizard.twoWords}</p>
        <div className="input-group">
          <input
            type="text"
            autoComplete="off"
            spellCheck={false}
            value={createPhrase}
            onChange={e => {
              setCreatePhrase(e.target.value)
              setCreatePhraseErr(false)
            }}
          />
          <div className={'error' + (createPhraseErr ? ' show' : '')}>
            {t.wizard.phraseError}
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() =>
            validatePhrase(createPhrase, setCreatePhraseErr, 'create-pin')
          }
        >
          {t.common.next}
        </button>
      </WizardShell>
    )
  }

  if (screen === 'create-pin') {
    return (
      <WizardShell step={2} back={() => goTo('create-phrase')}>
        <div className="step-label">{fmt(t.wizard.step, { n: 2 })}</div>
        <h2>{t.wizard.createPin}</h2>
        {dots(createPin.length)}
        <p className="hint">{t.wizard.createPinHint}</p>
        <div className="input-group">
          {pinInput(createPin, v => {
            setCreatePin(v)
            setCreatePinErr(false)
          })}
          <div className={'error' + (createPinErr ? ' show' : '')}>
            {t.wizard.pinError}
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => validatePin(createPin, setCreatePinErr, 'create-confirm-pin')}
        >
          {t.common.next}
        </button>
      </WizardShell>
    )
  }

  if (screen === 'create-confirm-pin') {
    return (
      <WizardShell step={3} back={() => goTo('create-pin')}>
        <div className="step-label">{fmt(t.wizard.step, { n: 3 })}</div>
        <h2>{t.wizard.confirmPinTitle}</h2>
        {dots(confirmPin.length)}
        <p className="hint">{t.wizard.confirmPinHint}</p>
        <div className="input-group">
          {pinInput(confirmPin, v => {
            setConfirmPin(v)
            setConfirmPinErr(null)
          })}
          <div className={'error' + (confirmPinErr ? ' show' : '')}>
            {confirmPinErr === 'mismatch' ? t.wizard.pinMismatch : t.wizard.pinError}
          </div>
        </div>
        <button className="btn btn-primary" onClick={confirmCreatePin}>
          {t.common.next}
        </button>
      </WizardShell>
    )
  }

  // create-done
  return (
    <WizardShell step={4} back={() => goTo('landing')}>
      <div className="key-icon">
        <IconKey />
      </div>
      <h2 style={{ marginBottom: 10 }}>{t.wizard.done}</h2>
      <p className="hint" style={{ marginBottom: 22 }}>
        {t.wizard.doneLine1}
        <br />
        {t.wizard.doneLine2}
      </p>
      <button className="btn btn-primary" onClick={() => rom()?.ExportKeyFile()}>
        <IconDownload />
        {t.wizard.downloadKey}
      </button>
      <button className="btn btn-secondary" style={{ marginTop: 10 }} onClick={() => goTo('landing')}>
        {t.wizard.toMain}
      </button>
    </WizardShell>
  )
}
