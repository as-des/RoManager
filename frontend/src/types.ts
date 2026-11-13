export type StatusKind = 'idle' | 'busy' | 'success' | 'danger'

export interface Status {
  text: string
  kind: StatusKind
}

/** Аккаунт в том виде, в котором его отдаёт C# (Bridge.Json / GetAccounts). */
export interface Account {
  id: number
  name: string
  online: boolean
  lastOnline: string
  dataOk: boolean
}

export type ManageMode = 'game' | 'player' | 'launcher' | null

export type Screen =
  | 'landing'
  | 'access-file'
  | 'access-phrase'
  | 'access-pin'
  | 'access-confirm'
  | 'create-phrase'
  | 'create-pin'
  | 'create-confirm-pin'
  | 'create-done'
  | 'dashboard'

/** Сырой ответ C#: поля приходят в camelCase. */
export interface AccountDto {
  id: number
  name: string
  online?: boolean
  lastOnline?: string | null
  dataOk?: boolean
}
