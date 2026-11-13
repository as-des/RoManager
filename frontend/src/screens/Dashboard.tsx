import { useCallback, useEffect, useRef, useState } from 'react'
import { rom } from '../bridge'
import { AddAccountModal } from '../components/AddAccountModal'
import { DeleteAccountModal } from '../components/DeleteAccountModal'
import {
  IconArrowRight,
  IconGamepad,
  IconGear,
  IconPlayer,
  IconPlay,
  IconPlus,
  IconRefresh,
  IconTrash,
} from '../components/icons'
import { StatusBar } from '../components/StatusBar'
import type { Account, AccountDto, ManageMode, Status, StatusKind } from '../types'

const mapAccounts = (list: AccountDto[] | null | undefined): Account[] =>
  (list ?? []).map(a => ({
    id: a.id,
    name: a.name,
    online: !!a.online,
    lastOnline: a.lastOnline || '—',
    dataOk: !!a.dataOk,
  }))

const IDLE_TEXT = 'Готов к работе'

interface Props {
  status: Status
  setStatus: (text: string, kind?: StatusKind) => void
  version: string
  onShowChangelog: () => void
}

export function Dashboard({ status, setStatus, version, onShowChangelog }: Props) {
  const [accounts, setAccounts] = useState<Account[]>([])
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [manageMode, setManageMode] = useState<ManageMode>(null)
  const [addOpen, setAddOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deleteText, setDeleteText] = useState('')

  const lastIndex = useRef(-1)
  const selectedRef = useRef(selected)
  const pendingDelete = useRef<number[]>([])
  const gameInput = useRef<HTMLInputElement>(null)
  const playerInput = useRef<HTMLInputElement>(null)

  selectedRef.current = selected

  const nameOf = useCallback(
    (id: number) => accounts.find(a => a.id === id)?.name ?? '—',
    [accounts],
  )

  const updateSelectionStatus = useCallback(
    (ids: Set<number>) => {
      if (ids.size === 0) setStatus(IDLE_TEXT)
      else if (ids.size === 1) setStatus(`Выбран аккаунт ${nameOf([...ids][0])}`)
      else setStatus(`Выбрано аккаунтов: ${ids.size}`)
    },
    [nameOf, setStatus],
  )

  // C# → JS
  useEffect(() => {
    window.__setAccounts = list => setAccounts(mapAccounts(list as AccountDto[]))
    window.__showAddAccountModal = () => setAddOpen(true)
    window.__closeAddAccountModal = () => setAddOpen(false)
    return () => {
      window.__setAccounts = undefined
      window.__showAddAccountModal = undefined
      window.__closeAddAccountModal = undefined
    }
  }, [])

  // первичная загрузка (на случай, если push пришёл до монтирования)
  useEffect(() => {
    let alive = true
    const bridge = rom()
    if (!bridge) return
    bridge
      .GetAccounts()
      .then(json => {
        if (alive) setAccounts(mapAccounts(JSON.parse(json) as AccountDto[]))
      })
      .catch(() => {})
    return () => {
      alive = false
    }
  }, [])

  // клик вне таблицы/сайдбара снимает выделение
  useEffect(() => {
    const onDocClick = (e: MouseEvent) => {
      const t = e.target as Element | null
      if (
        t?.closest(
          '.accounts-table, .sidebar, .dash-header, .settings-window, .modal-overlay',
        )
      ) {
        return
      }
      if (selectedRef.current.size > 0) {
        setSelected(new Set())
        lastIndex.current = -1
        setStatus(IDLE_TEXT)
      }
    }
    document.addEventListener('click', onDocClick)
    return () => document.removeEventListener('click', onDocClick)
  }, [setStatus])

  // панель «Управление» доступна только при одном выбранном аккаунте
  useEffect(() => {
    if (selected.size !== 1) setManageMode(null)
  }, [selected])

  // автофокус поля после раскрытия сегмента
  useEffect(() => {
    if (manageMode !== 'game' && manageMode !== 'player') return
    const t = window.setTimeout(() => {
      if (manageMode === 'game') gameInput.current?.focus()
      else playerInput.current?.focus()
    }, 300)
    return () => window.clearTimeout(t)
  }, [manageMode])

  const handleRowClick = (e: React.MouseEvent, id: number, index: number) => {
    e.stopPropagation()
    const next = new Set(selected)

    if (e.ctrlKey || e.metaKey) {
      if (next.has(id)) next.delete(id)
      else next.add(id)
      lastIndex.current = index
      updateSelectionStatus(next)
    } else if (e.shiftKey && lastIndex.current !== -1) {
      const start = Math.min(lastIndex.current, index)
      const end = Math.max(lastIndex.current, index)
      next.clear()
      for (let i = start; i <= end; i++) next.add(accounts[i].id)
      updateSelectionStatus(next)
    } else if (next.size === 1 && next.has(id)) {
      next.clear()
      lastIndex.current = -1
      setStatus(IDLE_TEXT)
    } else {
      next.clear()
      next.add(id)
      lastIndex.current = index
      updateSelectionStatus(next)
    }
    setSelected(next)
  }

  const executeManage = (mode: Exclude<ManageMode, null>) => {
    if (selected.size !== 1) return
    const accId = [...selected][0]
    const accName = nameOf(accId)

    if (mode === 'launcher') {
      setStatus(`Запуск лаунчера для ${accName}…`, 'busy')
      rom()?.Manage('launcher', '', accId)
      setManageMode(null)
      return
    }

    const input = mode === 'game' ? gameInput.current : playerInput.current
    const value = input?.value.trim() ?? ''
    if (!value) return

    setStatus(
      mode === 'game'
        ? `${accName} входит в игру ${value}…`
        : `${accName} подключается к игроку ${value}…`,
      'busy',
    )
    rom()?.Manage(mode, value, accId)
    if (input) input.value = ''
    setManageMode(null)
  }

  const askDelete = () => {
    if (selected.size === 0) return
    const names = [...selected].map(nameOf)
    pendingDelete.current = [...selected]
    setDeleteText(names.length === 1 ? `Аккаунт: ${names[0]}` : `Аккаунты: ${names.join(', ')}`)
    setDeleteOpen(true)
  }

  const confirmDelete = () => {
    rom()?.DeleteAccounts(JSON.stringify(pendingDelete.current))
    pendingDelete.current = []
    setSelected(new Set())
    lastIndex.current = -1
    setDeleteOpen(false)
  }

  const canManage = selected.size === 1
  const segment = (mode: Exclude<ManageMode, null>) =>
    'manage-segment' + (manageMode === mode ? ' active' : '')

  return (
    <div className="screen active">
      <div className="dashboard fade-in">
        <div className="dash-header">
          <button className="btn btn-ghost btn-sm" onClick={() => rom()?.Logout()}>
            Выйти
          </button>
        </div>

        <div className="dash-body">
          <div className="panel accounts-panel">
            <div className="panel-title">
              Аккаунты (<span>{accounts.length}</span>)
            </div>
            <table className="accounts-table-header">
              <colgroup>
                <col className="col-check" />
                <col />
                <col className="col-status" />
                <col className="col-launch" />
                <col className="col-data" />
              </colgroup>
              <thead>
                <tr>
                  <th></th>
                  <th>АККАУНТ</th>
                  <th>СТАТУС</th>
                  <th>ПОСЛЕДНИЙ ЗАПУСК</th>
                  <th>ДАННЫЕ</th>
                </tr>
              </thead>
            </table>

            <div className="accounts-scroll">
              <table className="accounts-table">
                <colgroup>
                  <col className="col-check" />
                  <col />
                  <col className="col-status" />
                  <col className="col-launch" />
                  <col className="col-data" />
                </colgroup>
                <tbody>
                  {accounts.map((acc, index) => {
                    const isSelected = selected.has(acc.id)
                    return (
                      <tr
                        key={acc.id}
                        className={isSelected ? 'selected' : undefined}
                        onClick={e => handleRowClick(e, acc.id, index)}
                      >
                        <td>
                          <div className={'cb' + (isSelected ? ' checked' : '')} />
                        </td>
                        <td>{acc.name}</td>
                        <td>
                          <span className="status">
                            <span
                              className={'status-dot ' + (acc.online ? 'online' : 'offline')}
                            />
                            {acc.online ? 'Активен' : 'Неактивен'}
                          </span>
                        </td>
                        <td>{acc.lastOnline}</td>
                        <td className={acc.dataOk ? 'data-ok' : 'data-old'}>
                          {acc.dataOk ? 'Актуальны' : 'Устарели · обновить'}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          </div>

          <div className="sidebar">
            <div className="panel">
              <div className="panel-title">Действия</div>
              <div className="actions-row">
                <button
                  className="btn btn-primary btn-add-acc"
                  onClick={() => rom()?.OpenAddAccount()}
                >
                  <IconPlus />
                  Добавить
                </button>
                <button
                  className="btn btn-secondary btn-icon"
                  disabled={selected.size !== 1}
                  title="Обновить данные"
                  onClick={() => {
                    if (selected.size === 1) rom()?.RefreshAccount([...selected][0])
                  }}
                >
                  <IconRefresh />
                </button>
                <button
                  className="btn btn-danger btn-icon"
                  disabled={selected.size === 0}
                  title="Удалить"
                  onClick={askDelete}
                >
                  <IconTrash />
                </button>
              </div>
            </div>

            <div className={'panel' + (canManage ? '' : ' disabled')}>
              <div className="panel-title">Управление аккаунтом</div>

              <div className={'manage-row' + (manageMode ? ' has-active' : '')}>
                <div
                  className={segment('game')}
                  onClick={e => {
                    e.stopPropagation()
                    if (canManage) setManageMode(m => (m === 'game' ? null : 'game'))
                  }}
                >
                  <div className="manage-icon-wrap">
                    <IconGamepad />
                  </div>
                  <div className="manage-content" onClick={e => e.stopPropagation()}>
                    <input ref={gameInput} type="text" placeholder="ID сервера" />
                    <button className="manage-submit" onClick={() => executeManage('game')}>
                      <IconArrowRight />
                    </button>
                  </div>
                </div>

                <div
                  className={segment('player')}
                  onClick={e => {
                    e.stopPropagation()
                    if (canManage) setManageMode(m => (m === 'player' ? null : 'player'))
                  }}
                >
                  <div className="manage-icon-wrap">
                    <IconPlayer />
                  </div>
                  <div className="manage-content" onClick={e => e.stopPropagation()}>
                    <input ref={playerInput} type="text" placeholder="Ник игрока" />
                    <button className="manage-submit" onClick={() => executeManage('player')}>
                      <IconArrowRight />
                    </button>
                  </div>
                </div>

                <div
                  className={segment('launcher')}
                  onClick={e => {
                    e.stopPropagation()
                    if (!canManage) return
                    if (manageMode === 'launcher') executeManage('launcher')
                    else setManageMode('launcher')
                  }}
                >
                  <div className="manage-icon-wrap launcher-btn">
                    <IconPlay />
                  </div>
                  <div className="manage-content">
                    <span className="manage-label" style={{ paddingLeft: 4 }}>
                      Запуск лаунчера
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <div className="panel">
              <div className="panel-title">Настройки</div>
              <div className="actions-list">
                <button className="btn btn-secondary" onClick={() => rom()?.OpenSettings()}>
                  <IconGear />
                  Открыть настройки
                </button>
              </div>
            </div>
          </div>
        </div>

        <StatusBar
          status={status}
          version={version}
          onShowChangelog={onShowChangelog}
        />
      </div>

      <AddAccountModal open={addOpen} onClose={() => setAddOpen(false)} />
      <DeleteAccountModal
        open={deleteOpen}
        text={deleteText}
        onConfirm={confirmDelete}
        onCancel={() => setDeleteOpen(false)}
      />
    </div>
  )
}
