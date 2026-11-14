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
import { LanguageSwitch } from '../components/LanguageSwitch'
import { fmt, useI18n } from '../i18n'
import type { Account, AccountDto, ManageMode, Status, StatusKind } from '../types'

const mapAccounts = (list: AccountDto[] | null | undefined): Account[] =>
  (list ?? []).map(a => ({
    id: a.id,
    name: a.name,
    online: !!a.online,
    lastOnline: a.lastOnline || '—',
    dataOk: !!a.dataOk,
  }))

interface Props {
  status: Status
  setStatus: (text: string, kind?: StatusKind) => void
  version: string
  onShowChangelog: () => void
}

export function Dashboard({ status, setStatus, version, onShowChangelog }: Props) {
  const { t } = useI18n()
  const idleText = t.dash.ready
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
      if (ids.size === 0) setStatus(idleText)
      else if (ids.size === 1) setStatus(fmt(t.dash.selectedOne, { name: nameOf([...ids][0]) }))
      else setStatus(fmt(t.dash.selectedMany, { count: ids.size }))
    },
    [idleText, nameOf, setStatus, t],
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
        setStatus(idleText)
      }
    }
    document.addEventListener('click', onDocClick)
    return () => document.removeEventListener('click', onDocClick)
  }, [idleText, setStatus])

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
      setStatus(idleText)
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
      setStatus(fmt(t.dash.launchingLauncher, { name: accName }), 'busy')
      rom()?.Manage('launcher', '', accId)
      setManageMode(null)
      return
    }

    const input = mode === 'game' ? gameInput.current : playerInput.current
    const value = input?.value.trim() ?? ''
    if (!value) return

    setStatus(
      mode === 'game'
        ? fmt(t.dash.enteringGame, { name: accName, value })
        : fmt(t.dash.joiningPlayer, { name: accName, value }),
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
    setDeleteText(
      names.length === 1
        ? fmt(t.dash.deleteOne, { name: names[0] })
        : fmt(t.dash.deleteMany, { names: names.join(', ') }),
    )
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
          <div className="dash-header-left">
            <LanguageSwitch />
          </div>
          <button className="btn btn-ghost btn-sm" onClick={() => rom()?.Logout()}>
            {t.dash.logout}
          </button>
        </div>

        <div className="dash-body">
          <div className="panel accounts-panel">
            <div className="panel-title">
              {t.dash.accounts} (<span>{accounts.length}</span>)
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
                  <th>{t.dash.colAccount}</th>
                  <th>{t.dash.colStatus}</th>
                  <th>{t.dash.colLast}</th>
                  <th>{t.dash.colData}</th>
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
                            {acc.online ? t.dash.online : t.dash.offline}
                          </span>
                        </td>
                        <td>{acc.lastOnline}</td>
                        <td className={acc.dataOk ? 'data-ok' : 'data-old'}>
                          {acc.dataOk ? t.dash.dataOk : t.dash.dataOld}
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
              <div className="panel-title">{t.dash.actions}</div>
              <div className="actions-row">
                <button
                  className="btn btn-primary btn-add-acc"
                  onClick={() => rom()?.OpenAddAccount()}
                >
                  <IconPlus />
                  {t.dash.add}
                </button>
                <button
                  className="btn btn-secondary btn-icon"
                  disabled={selected.size !== 1}
                  title={t.dash.refresh}
                  onClick={() => {
                    if (selected.size === 1) rom()?.RefreshAccount([...selected][0])
                  }}
                >
                  <IconRefresh />
                </button>
                <button
                  className="btn btn-danger btn-icon"
                  disabled={selected.size === 0}
                  title={t.dash.delete}
                  onClick={askDelete}
                >
                  <IconTrash />
                </button>
              </div>
            </div>

            <div className={'panel' + (canManage ? '' : ' disabled')}>
              <div className="panel-title">{t.dash.manage}</div>

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
                    <input ref={gameInput} type="text" placeholder={t.dash.serverId} />
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
                    <input ref={playerInput} type="text" placeholder={t.dash.playerNick} />
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
                      {t.dash.launchLauncher}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <div className="panel">
              <div className="panel-title">{t.dash.settings}</div>
              <div className="actions-list">
                <button className="btn btn-secondary" onClick={() => rom()?.OpenSettings()}>
                  <IconGear />
                  {t.dash.openSettings}
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
