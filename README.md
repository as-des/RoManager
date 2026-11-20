<div align="center">

<h1 align="center">
  <img src="assets/app-converted.png" alt="RoManager Icon" width="48" height="48" style="vertical-align: middle; margin-right: 10px;">
  <span style="vertical-align: middle;">RoManager</span>
</h1>

**Менеджер аккаунтов Roblox для Windows**  
Шифрованное хранилище, быстрый запуск игры, управление cookie и современный интерфейс на C# / WinUI 3.

<!-- Бейджи -->
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=for-the-badge&logo=dotnet)]()
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue?style=for-the-badge&logo=windows)]()
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)]()
[![Version](https://img.shields.io/badge/Version-0.2.0-orange?style=for-the-badge)]()

> ⚠️ **Не аффилировано с Roblox Corporation.** Приложение не связано с Roblox и использует только публичные API и cookie ваших собственных аккаунтов. Автоматизация нескольких аккаунтов может нарушать правила Roblox — используйте на свой риск.

</div>

---

## 📖 О проекте

**RoManager** — это удобное приложение для управления несколькими аккаунтами Roblox. Оно позволяет безопасно хранить сессии, быстро переключаться между профилями и запускать игру в один клик.

### ✨ Ключевые возможности

<table>
  <tr>
    <td width="50%">
      <h4>👤 Аккаунты</h4>
      <ul>
        <li>Добавление через встроенный браузер (Chrome/Edge, CDP) или вставку <code>.ROBLOSECURITY</code> cookie.</li>
        <li>Множественное выделение, удаление и обновление данных.</li>
        <li><b>Избранное:</b> звезда, золотая подсветка, поднятие в начало списка.</li>
        <li>Поиск по имени и сортировка по любому столбцу.</li>
        <li>Статус онлайн/офлайн и время последнего запуска.</li>
      </ul>
    </td>
    <td width="50%">
      <h4>🚀 Запуск</h4>
      <ul>
        <li>Запуск лаунчера Roblox или конкретной игры по <code>placeId</code>.</li>
        <li>Подключение к серверу по ID и «К игроку» по нику.</li>
        <li>Multi Roblox (несколько клиентов одновременно).</li>
        <li>Разблокировка FPS.</li>
      </ul>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <h4>🔐 Безопасность</h4>
      <ul>
        <li>Хранилище шифруется <b>AES-256-GCM</b>, ключ через <b>PBKDF2-SHA256 (600 000 итераций)</b>.</li>
        <li>Поддержка старых хранилищ (AES-256-CBC).</li>
        <li><b>Сохранение сессии:</b> ключевой файл и код авторизации шифруются DPAPI (срок 1–365 дней).</li>
        <li>Пароль Roblox нигде не хранится — только cookie.</li>
      </ul>
    </td>
    <td width="50%">
      <h4>🎨 Интерфейс</h4>
      <ul>
        <li>Нативный WinUI 3 с матовым стеклом и анимированным фоном.</li>
        <li>Шейдер на GPU (HLSL через Win2D) с fallback на CPU.</li>
        <li>Русский и английский язык.</li>
        <li>История обновлений и проверка релизов на GitHub.</li>
      </ul>
    </td>
  </tr>
</table>

---

## 📸 Скриншоты

<!-- ЗАГЛУШКА: Замените ссылку на свой скриншот позже -->
<div align="center">
  <img src="https://placehold.co/800x450/1e1e2e/ffffff?text=RoManager+%7C+%D0%93%D0%BB%D0%B0%D0%B2%D0%BD%D0%BE%D0%B5+%D0%BC%D0%B5%D0%BD%D1%8E&font=montserrat" alt="Главное меню RoManager" width="800" style="border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.3);">
  <br>
  <sub>Главное меню (Дашборд)</sub>
</div>

<br>

<!-- Место для второго скриншота (если нужно) -->
<!--
<div align="center">
  <img src="assets/screenshots/settings.png" alt="Настройки" width="800" style="border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.3);">
  <br>
  <sub>Настройки приложения</sub>
</div>
-->

---

## 🛠️ Требования и быстрый старт

### Требования
- Windows 10 x64 (1809+) или Windows 11.
- Портативная сборка **не требует** установки .NET или Windows App SDK.
- Для входа через браузер нужен Chrome или Edge (можно положить портативный `chrome-win64` рядом с `RoManager.exe`).

### Быстрый старт
1. Скачайте архив релиза из [Releases](https://github.com/as-des/RoManager/releases) и распакуйте в любую папку.
2. Запустите `RoManager.exe`.
3. Создайте новый ключевой файл либо откройте существующий `.json`, введите фразу и PIN.
4. Добавьте аккаунты кнопкой «Добавить» на дашборде.

> 💡 Все данные хранятся рядом с `RoManager.exe`. Папку можно носить на флешке; ключевой файл рекомендуется держать отдельно от хранилища.

---

## 📂 Файлы данных

| Файл | Назначение |
| :--- | :--- |
| `AccountData.json` | Зашифрованное хранилище аккаунтов и настроек |
| `AccountData.json.bak` | Резервная копия предыдущего состояния |
| `manager-session.json` | Сохранённая сессия (DPAPI), если включена |
| `shader.json` | Настройки анимированного фона |
| `ui.json` | Выбранный язык интерфейса |
| `rom.log` | Журнал работы (ротация при 2 МБ) |

*Файлы с cookie, сессией, ключами и логами намеренно исключены из Git.*

---

## 🧱 Сборка из исходников

Нужен [.NET 8 SDK](https://dotnet.microsoft.com/download) и Windows 10/11 x64.

```powershell
# Debug
dotnet build src\RoManager.App\RoManager.App.csproj -c Debug -p:Platform=x64

# Release / портативная сборка
dotnet publish src\RoManager.App\RoManager.App.csproj `
  -c Release -r win-x64 --self-contained true -p:Platform=x64 -o publish
