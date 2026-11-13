import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { rom } from '../bridge'
import { IconBack, IconDownload, IconKey, IconUpload } from '../components/icons'
import type { Screen } from '../types'

interface Props {
  screen: Exclude<Screen, 'dashboard'>
  goTo: (screen: Screen) => void
}

/** Шапка мастера: «Назад» + точки прогресса (1..4). */
function WizardShell({
  step,
  back,
  children,
}: {
  step: number
  back: () => void
  children: ReactNode
}) {
  return (
    <div className="screen wizard-layout active">
      <div className="wizard-header">
        <button className="back-btn" onClick={back}>
          <IconBack />
          Назад
        </button>
        <div className="progress">
          {[1, 2, 3, 4].map(i => (
            <div
              key={i}
              className={'progress-dot' + (i < step ? ' done' : i === step ? ' active' : '')}
            />
          ))}
        </div>
      </div>
      <div className="card fade-in">{children}</div>
    </div>
  )
}

export function Wizard({ screen, goTo }: Props) {
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
  const [confirmPinErr, setConfirmPinErr] = useState<string | null>(null)

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
      setConfirmPinErr('PIN должен содержать от 4 до 6 цифр.')
      return
    }
    if (createPin !== confirmPin) {
      setConfirmPinErr('PIN не совпадает.')
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
        <div className="landing fade-in" style={{ paddingTop: 40 }}>
          <button className="btn btn-primary" onClick={startAccess}>
            У меня есть доступ
          </button>
          <button className="btn btn-secondary" style={{ marginTop: 10 }} onClick={startCreate}>
            У меня нет доступа
          </button>
        </div>
      </div>
    )
  }

  if (screen === 'access-file') {
    return (
      <WizardShell step={1} back={() => goTo('landing')}>
        <div className="step-label">Шаг 1 / 4</div>
        <h2>Ключевой файл</h2>
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
          <p>Перетащите .json файл или нажмите</p>
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
          Далее
        </button>
      </WizardShell>
    )
  }

  if (screen === 'access-phrase') {
    return (
      <WizardShell step={2} back={() => goTo('access-file')}>
        <div className="step-label">Шаг 2 / 4</div>
        <h2>Ключевая фраза</h2>
        <p className="hint">Два слова через пробел</p>
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
            Фраза должна состоять ровно из двух слов.
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => validatePhrase(accessPhrase, setAccessPhraseErr, 'access-pin')}
        >
          Далее
        </button>
      </WizardShell>
    )
  }

  if (screen === 'access-pin') {
    return (
      <WizardShell step={3} back={() => goTo('access-phrase')}>
        <div className="step-label">Шаг 3 / 4</div>
        <h2>PIN-код</h2>
        {dots(accessPin.length)}
        <p className="hint">PIN-код (4–6 цифр)</p>
        <div className="input-group">
          {pinInput(accessPin, v => {
            setAccessPin(v)
            setAccessPinErr(false)
          })}
          <div className={'error' + (accessPinErr ? ' show' : '')}>
            PIN должен содержать от 4 до 6 цифр.
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => validatePin(accessPin, setAccessPinErr, 'access-confirm')}
        >
          Далее
        </button>
      </WizardShell>
    )
  }

  if (screen === 'access-confirm') {
    return (
      <WizardShell step={4} back={() => goTo('access-pin')}>
        <div className="step-label">Шаг 4 / 4</div>
        <h2>Подтверждение</h2>
        <div className="summary">
          <div className="summary-row">
            <span className="label">Файл</span>
            <span className="value">{keyName || 'key.json'}</span>
          </div>
          <div className="summary-row">
            <span className="label">Фраза</span>
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
          Подтвердить вход
        </button>
      </WizardShell>
    )
  }

  if (screen === 'create-phrase') {
    return (
      <WizardShell step={1} back={() => goTo('landing')}>
        <div className="step-label">Шаг 1 / 4</div>
        <h2>Создайте ключевую фразу</h2>
        <p className="hint">Два слова через пробел</p>
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
            Фраза должна состоять ровно из двух слов.
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() =>
            validatePhrase(createPhrase, setCreatePhraseErr, 'create-pin')
          }
        >
          Далее
        </button>
      </WizardShell>
    )
  }

  if (screen === 'create-pin') {
    return (
      <WizardShell step={2} back={() => goTo('create-phrase')}>
        <div className="step-label">Шаг 2 / 4</div>
        <h2>Создайте PIN-код</h2>
        {dots(createPin.length)}
        <p className="hint">PIN-код (4–6 цифр). Запомните его!</p>
        <div className="input-group">
          {pinInput(createPin, v => {
            setCreatePin(v)
            setCreatePinErr(false)
          })}
          <div className={'error' + (createPinErr ? ' show' : '')}>
            PIN должен содержать от 4 до 6 цифр.
          </div>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => validatePin(createPin, setCreatePinErr, 'create-confirm-pin')}
        >
          Далее
        </button>
      </WizardShell>
    )
  }

  if (screen === 'create-confirm-pin') {
    return (
      <WizardShell step={3} back={() => goTo('create-pin')}>
        <div className="step-label">Шаг 3 / 4</div>
        <h2>Подтвердите PIN</h2>
        {dots(confirmPin.length)}
        <p className="hint">Введите PIN ещё раз</p>
        <div className="input-group">
          {pinInput(confirmPin, v => {
            setConfirmPin(v)
            setConfirmPinErr(null)
          })}
          <div className={'error' + (confirmPinErr ? ' show' : '')}>
            {confirmPinErr ?? 'PIN не совпадает.'}
          </div>
        </div>
        <button className="btn btn-primary" onClick={confirmCreatePin}>
          Далее
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
      <h2 style={{ marginBottom: 10 }}>Готово!</h2>
      <p className="hint" style={{ marginBottom: 22 }}>
        Ключевой файл создан.
        <br />
        Сохраните его вместе с фразой и PIN.
      </p>
      <button className="btn btn-primary" onClick={() => rom()?.ExportKeyFile()}>
        <IconDownload />
        Скачать ключевой файл
      </button>
      <button className="btn btn-secondary" style={{ marginTop: 10 }} onClick={() => goTo('landing')}>
        На главный экран
      </button>
    </WizardShell>
  )
}
