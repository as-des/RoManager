/**
 * Типизированная обёртка над C#-мостом WebView2 (Bridge.cs).
 * Контракт заморожен: имена методов должны совпадать с public-методами Bridge.
 */
export interface RomBridge {
  // окно
  Minimize(): void
  ToggleMax(): void
  CloseApp(): void
  BeginResize(edge: string): void
  EndResize(): void
  // данные
  GetAccounts(): Promise<string>
  // аккаунты
  OpenAddAccount(): void
  OpenBrowserLogin(): void
  AddAccountManual(cookie: string): void
  RefreshAccount(id: number): void
  DeleteAccounts(idsJson: string): void
  Manage(mode: string, value: string, accId: number): void
  // авторизация
  CompleteAccess(keyContent: string | null, phrase: string, pin: string): void
  CreateKeyFile(phrase: string, pin: string, pinConfirm: string): void
  ExportKeyFile(): void
  // прочее
  OpenSettings(): void
  GetUiLanguage(): Promise<string>
  SetUiLanguage(lang: string): void
  GetSettings(): Promise<string>
  SaveSettings(json: string): Promise<void>
  GetShaderSettings(): Promise<string>
  SaveShaderSettings(json: string): void
  GetVersion(): Promise<string>
  DragSettings(): void
  CloseSettingsWindow(): void
  Logout(): void
}

declare global {
  interface Window {
    chrome?: {
      webview?: {
        hostObjects?: { rom?: RomBridge }
        postMessage(message: string): void
      }
    }
    // обратные вызовы C# → JS (Bridge.Push)
    __setAccounts?: (list: unknown) => void
    __setStatus?: (text: string, kind?: string) => void
    __enterDashboard?: () => void
    __showAddAccountModal?: () => void
    __closeAddAccountModal?: () => void
    __keyFileDropped?: (d: { name: string; content: string | null }) => void
    goTo?: (id: string) => void
    openModal?: (title: string, text: string) => void
    __notify?: (
      text: string,
      kind?: string,
      actions?: { label: string; id: string }[],
    ) => void
    __applyShaderSettings?: (settings: unknown) => void
    __applyLanguage?: (lang: string) => void
    __setResizing?: (active: boolean) => void
  }
}

export function rom(): RomBridge | null {
  return window.chrome?.webview?.hostObjects?.rom ?? null
}

export function dragWindow(): void {
  window.chrome?.webview?.postMessage('drag')
}
