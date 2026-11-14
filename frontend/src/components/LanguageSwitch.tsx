import { useI18n } from '../i18n'
import type { Lang } from '../i18n'

/** Компактный переключатель языка RU/EN. Используется на всех экранах. */
export function LanguageSwitch({ className = '' }: { className?: string }) {
  const { lang, setLang, t } = useI18n()

  const button = (value: Lang) => (
    <button
      type="button"
      className={'lang-btn' + (lang === value ? ' active' : '')}
      onClick={() => setLang(value)}
      aria-pressed={lang === value}
    >
      {value.toUpperCase()}
    </button>
  )

  return (
    <div className={'lang-switch ' + className} role="group" aria-label={t.common.language}>
      {button('ru')}
      {button('en')}
    </div>
  )
}
