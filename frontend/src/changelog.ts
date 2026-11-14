/** История обновлений. Показывается по кнопке версии в нижней панели. */
export interface ChangelogEntry {
  version: string
  date: string
  dateEn: string
  changes: string[]
  changesEn: string[]
}

export const CHANGELOG: ChangelogEntry[] = [
  {
    version: '2.1.0',
    date: '13 ноября 2026',
    dateEn: 'November 13, 2026',
    changes: [
      'Главное окно и окно настроек получили нативную рамку Windows: системный заголовок, кнопки и плавный ресайз.',
      'Интерфейс адаптируется под размер окна: dashboard перестраивается в одну колонку, таблица и нижняя панель подстраиваются.',
      'Уведомления, ошибки и информация выводятся плашкой в верхней части окна: действия в ряд и кнопка закрытия.',
      'В нижней панели появилась кнопка версии — по клику открывается это окно с историей обновлений.',
      'История обновлений доступна на русском и английском (переключатель RU/EN), пункты отмечены прочерками.',
      'Новые хранилища шифруются AES-256-GCM (PBKDF2-SHA256, 600 000 итераций). Старые хранилища открываются и сохраняются в прежнем формате.',
      'Multi Roblox, автообновление cookie, встроенный Chrome и выключение анимированного фона в настройках.',
      'Усилена защита WebView2: в релизе отключены DevTools и внешняя навигация, добавлены CSP и защита от path traversal.',
      'Портативная сборка win-x64 с контрольной суммой SHA-256, CI и релизный workflow.',
    ],
    changesEn: [
      'The main window and the settings window now use the native Windows frame: system title bar, window buttons and smooth resizing.',
      'The layout adapts to any window size: the dashboard collapses to a single column and the table and status bar adjust.',
      'Notifications, errors and information appear as banners at the top of the window with inline actions and a close button.',
      'A version button in the bottom bar opens this changelog window.',
      'The changelog is available in Russian and English (RU/EN switch) and uses dashes instead of coloured bullets.',
      'New vaults are encrypted with AES-256-GCM (PBKDF2-SHA256, 600,000 iterations). Legacy vaults open and stay in the old format.',
      'Multi Roblox, automatic cookie refresh, bundled Chrome and an animated background toggle in settings.',
      'Hardened WebView2: DevTools and external navigation disabled in release builds, CSP and path traversal protection added.',
      'Portable win-x64 build with SHA-256 checksums, CI and a release workflow.',
    ],
  },
  {
    version: '2.0.0',
    date: '7 ноября 2026',
    dateEn: 'November 7, 2026',
    changes: [
      'Новый интерфейс на React + TypeScript (Vite) по исходному макету.',
      'Матовое стекло, анимированный GLSL-фон с тремя цветами и регулировкой скорости.',
      'Безрамочное окно с ресайзом за края и отдельное окно настроек.',
      'Приватный режим логов и ротация лога.',
    ],
    changesEn: [
      'New React + TypeScript (Vite) interface based on the original design.',
      'Frosted glass, an animated GLSL background with three colours and speed control.',
      'Borderless window with edge resizing and a separate settings window.',
      'Private log mode and log rotation.',
    ],
  },
]
