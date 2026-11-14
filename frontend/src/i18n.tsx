import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { rom } from './bridge'

export type Lang = 'ru' | 'en'

/** Все строки интерфейса. en обязан повторять структуру ru. */
const ru = {
  common: {
    back: 'Назад',
    next: 'Далее',
    cancel: 'Отмена',
    close: 'Закрыть',
    save: 'Сохранить настройки',
    language: 'Язык',
    interface: 'Интерфейс',
  },
  wizard: {
    haveAccess: 'У меня есть доступ',
    noAccess: 'У меня нет доступа',
    step: 'Шаг {n} / 4',
    keyFile: 'Ключевой файл',
    dropHint: 'Перетащите .json файл или нажмите',
    keyPhrase: 'Ключевая фраза',
    twoWords: 'Два слова через пробел',
    phraseError: 'Фраза должна состоять ровно из двух слов.',
    pin: 'PIN-код',
    pinHint: 'PIN-код (4–6 цифр)',
    pinError: 'PIN должен содержать от 4 до 6 цифр.',
    confirm: 'Подтверждение',
    file: 'Файл',
    phrase: 'Фраза',
    confirmLogin: 'Подтвердить вход',
    createPhrase: 'Создайте ключевую фразу',
    createPin: 'Создайте PIN-код',
    createPinHint: 'PIN-код (4–6 цифр). Запомните его!',
    confirmPinTitle: 'Подтвердите PIN',
    confirmPinHint: 'Введите PIN ещё раз',
    pinMismatch: 'PIN не совпадает.',
    done: 'Готово!',
    doneLine1: 'Ключевой файл создан.',
    doneLine2: 'Сохраните его вместе с фразой и PIN.',
    downloadKey: 'Скачать ключевой файл',
    toMain: 'На главный экран',
    keyJson: 'key.json',
    interfaceTitle: 'Настройки интерфейса',
    animatedBg: 'Анимированный фон',
  },
  dash: {
    logout: 'Выйти',
    accounts: 'Аккаунты',
    colAccount: 'АККАУНТ',
    colStatus: 'СТАТУС',
    colLast: 'ПОСЛЕДНИЙ ЗАПУСК',
    colData: 'ДАННЫЕ',
    online: 'Активен',
    offline: 'Неактивен',
    dataOk: 'Актуальны',
    dataOld: 'Устарели · обновить',
    actions: 'Действия',
    add: 'Добавить',
    refresh: 'Обновить данные',
    delete: 'Удалить',
    manage: 'Управление аккаунтом',
    serverId: 'ID сервера',
    playerNick: 'Ник игрока',
    launchLauncher: 'Запуск лаунчера',
    settings: 'Настройки',
    openSettings: 'Открыть настройки',
    ready: 'Готов к работе',
    selectedOne: 'Выбран аккаунт {name}',
    selectedMany: 'Выбрано аккаунтов: {count}',
    launchingLauncher: 'Запуск лаунчера для {name}…',
    enteringGame: '{name} входит в игру {value}…',
    joiningPlayer: '{name} подключается к игроку {value}…',
    deleteOne: 'Аккаунт: {name}',
    deleteMany: 'Аккаунты: {names}',
    deleteHeading: 'Удалить аккаунты?',
    changelog: 'История обновлений',
  },
  add: {
    title: 'Добавить аккаунт',
    hint: 'Войти через браузер Roblox или вставить cookie вручную.',
    browser: 'Войти через браузер',
    cookie: '.ROBLOSECURITY cookie (необязательно)',
    byCookie: 'Добавить по cookie',
  },
  notice: { close: 'Закрыть уведомление' },
  settings: {
    tabs: { general: 'Общие', developer: 'Разработчик', misc: 'Разное' },
    session: 'Сессия и аккаунты',
    autoCookie: 'Автоматически обновлять cookie',
    refreshInterval: 'Интервал обновления (мин)',
    launchDelay: 'Задержка запуска, мс',
    closePrev: 'Закрывать предыдущий Roblox перед запуском',
    multiTitle: 'Multi Roblox',
    multiLabel: 'Разрешить несколько клиентов Roblox одновременно',
    multiHint:
      'Включайте при закрытом Roblox: приложение удерживает системный мьютекс ROBLOX_singletonMutex. Если Roblox уже запущен, настройка применится после его закрытия и повторного сохранения.',
    appliedHint: 'Некоторые настройки применяются сразу, другие — при следующем запуске.',
    debug: 'Отладка',
    devLog: 'Писать подробный лог (rom.log)',
    devLogHint: 'Влияет на мировые исключения и вывода в файл лога.',
    presence: 'Presence',
    showPresence: 'Показывать присутствие',
    updateSec: 'Обновление (сек)',
    unlockFps: 'Разблокировать FPS',
    maxFps: 'Макс FPS',
    browser: 'Браузер',
    bundled: 'Встраивать Chrome (chrome-win64), если он рядом',
    bundledHint: 'Если выключено или папки нет, используется установленный Chrome, затем Edge.',
    background: 'Фон (GLSL)',
    animated: 'Анимированный фон',
    waveColors: 'Цвета волн',
    wave1: 'Волна 1',
    wave2: 'Волна 2',
    wave3: 'Волна 3',
    speed: 'Скорость',
    speedHint:
      'Скорость анимации от {min} до {max}; 1.00× — как в оригинале. Фон применяется сразу после сохранения.',
    interface: 'Интерфейс',
    language: 'Язык интерфейса',
  },
  changelog: { title: 'История обновлений' },
}

const en: typeof ru = {
  common: {
    back: 'Back',
    next: 'Next',
    cancel: 'Cancel',
    close: 'Close',
    save: 'Save settings',
    language: 'Language',
    interface: 'Interface',
  },
  wizard: {
    haveAccess: 'I have access',
    noAccess: "I don't have access",
    step: 'Step {n} / 4',
    keyFile: 'Key file',
    dropHint: 'Drop a .json file or click',
    keyPhrase: 'Key phrase',
    twoWords: 'Two words separated by a space',
    phraseError: 'The phrase must contain exactly two words.',
    pin: 'PIN code',
    pinHint: 'PIN code (4–6 digits)',
    pinError: 'PIN must contain 4 to 6 digits.',
    confirm: 'Confirmation',
    file: 'File',
    phrase: 'Phrase',
    confirmLogin: 'Confirm sign-in',
    createPhrase: 'Create a key phrase',
    createPin: 'Create a PIN code',
    createPinHint: 'PIN code (4–6 digits). Remember it!',
    confirmPinTitle: 'Confirm the PIN',
    confirmPinHint: 'Enter the PIN again',
    pinMismatch: 'PINs do not match.',
    done: 'Done!',
    doneLine1: 'The key file has been created.',
    doneLine2: 'Keep it together with the phrase and PIN.',
    downloadKey: 'Download key file',
    toMain: 'Back to main screen',
    keyJson: 'key.json',
    interfaceTitle: 'Interface settings',
    animatedBg: 'Animated background',
  },
  dash: {
    logout: 'Log out',
    accounts: 'Accounts',
    colAccount: 'ACCOUNT',
    colStatus: 'STATUS',
    colLast: 'LAST LAUNCH',
    colData: 'DATA',
    online: 'Online',
    offline: 'Offline',
    dataOk: 'Up to date',
    dataOld: 'Outdated · refresh',
    actions: 'Actions',
    add: 'Add',
    refresh: 'Refresh data',
    delete: 'Delete',
    manage: 'Account management',
    serverId: 'Server ID',
    playerNick: 'Player nickname',
    launchLauncher: 'Launch launcher',
    settings: 'Settings',
    openSettings: 'Open settings',
    ready: 'Ready',
    selectedOne: 'Selected account {name}',
    selectedMany: 'Selected accounts: {count}',
    launchingLauncher: 'Launching the launcher for {name}…',
    enteringGame: '{name} is joining game {value}…',
    joiningPlayer: '{name} is connecting to player {value}…',
    deleteOne: 'Account: {name}',
    deleteMany: 'Accounts: {names}',
    deleteHeading: 'Delete accounts?',
    changelog: 'Changelog',
  },
  add: {
    title: 'Add account',
    hint: 'Sign in through the Roblox browser or paste a cookie manually.',
    browser: 'Sign in via browser',
    cookie: '.ROBLOSECURITY cookie (optional)',
    byCookie: 'Add by cookie',
  },
  notice: { close: 'Dismiss notification' },
  settings: {
    tabs: { general: 'General', developer: 'Developer', misc: 'Miscellaneous' },
    session: 'Session & accounts',
    autoCookie: 'Refresh cookies automatically',
    refreshInterval: 'Refresh interval (min)',
    launchDelay: 'Launch delay, ms',
    closePrev: 'Close the previous Roblox before launch',
    multiTitle: 'Multi Roblox',
    multiLabel: 'Allow multiple Roblox clients at once',
    multiHint:
      'Enable while Roblox is closed: the app holds the ROBLOX_singletonMutex system mutex. If Roblox is already running, the setting applies after you close it and save again.',
    appliedHint: 'Some settings apply immediately, others on the next launch.',
    debug: 'Debugging',
    devLog: 'Write a detailed log (rom.log)',
    devLogHint: 'Affects global exceptions and log file output.',
    presence: 'Presence',
    showPresence: 'Show presence',
    updateSec: 'Update (sec)',
    unlockFps: 'Unlock FPS',
    maxFps: 'Max FPS',
    browser: 'Browser',
    bundled: 'Use bundled Chrome (chrome-win64) when present',
    bundledHint: 'If disabled or missing, the installed Chrome is used, then Edge.',
    background: 'Background (GLSL)',
    animated: 'Animated background',
    waveColors: 'Wave colours',
    wave1: 'Wave 1',
    wave2: 'Wave 2',
    wave3: 'Wave 3',
    speed: 'Speed',
    speedHint:
      'Animation speed from {min} to {max}; 1.00× is the original. The background applies right after saving.',
    interface: 'Interface',
    language: 'Interface language',
  },
  changelog: { title: 'Changelog' },
}

export type Dict = typeof ru

export function fmt(template: string, vars: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (_, key: string) =>
    vars[key] === undefined ? `{${key}}` : String(vars[key]),
  )
}

const STORAGE_KEY = 'rom.lang'

function readStored(): Lang {
  try {
    const v = localStorage.getItem(STORAGE_KEY)
    if (v === 'ru' || v === 'en') return v
  } catch {
    /* localStorage может быть недоступен */
  }
  return 'ru'
}

interface I18nValue {
  lang: Lang
  setLang: (lang: Lang) => void
  t: Dict
}

const I18nContext = createContext<I18nValue | null>(null)

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(readStored)

  const applyLang = useCallback((next: Lang) => {
    setLangState(next)
    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      /* ignore */
    }
    document.documentElement.lang = next
  }, [])

  // Язык хранится в C# (ui.json) и доступен до входа;
  // localStorage нужен только для мгновенного старта без мигания.
  useEffect(() => {
    let alive = true
    rom()
      ?.GetUiLanguage()
      .then(value => {
        if (alive && (value === 'ru' || value === 'en') && value !== lang) applyLang(value)
      })
      .catch(() => {})

    const onApply = (value: string) => {
      if (value === 'ru' || value === 'en') applyLang(value)
    }
    window.__applyLanguage = onApply
    return () => {
      alive = false
      if (window.__applyLanguage === onApply) window.__applyLanguage = undefined
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [applyLang])

  const setLang = useCallback(
    (next: Lang) => {
      applyLang(next)
      rom()?.SetUiLanguage(next)
    },
    [applyLang],
  )

  const t = lang === 'en' ? en : ru
  const value = useMemo<I18nValue>(() => ({ lang, setLang, t }), [lang, setLang, t])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useI18n(): I18nValue {
  const value = useContext(I18nContext)
  if (!value) throw new Error('useI18n must be used inside LanguageProvider')
  return value
}
