# Релиз

## Один раз

1. Создайте репозиторий на GitHub (например `as-des/RoManager`), ветка `main`.
2. Проверьте, что в git **не попадут** секреты и данные:
   ```
   git status --ignored
   ```
   В индексе не должно быть `rom-key.json`, `AccountData.json*`, `rom.log*`,
   `shader.json`, `wv2/`, `chrome-win64/`, `bin/`, `obj/`, `wwwroot/`.
3. Включите Actions (Settings → Actions → General) и private vulnerability reporting
   (Settings → Security).

## Нумерация версий

`MAJOR.MINOR.PATCH`, тег с префиксом `v`: `v2.1.0`.

- `PATCH` — исправления;
- `MINOR` — новые функции без ломки данных;
- `MAJOR` — смена формата ключей/хранилища или несовместимый перенос.

`frontend/package.json` → `version` и тег держите синхронно.

## Выпуск

```bat
cd frontend
npm ci
npm run typecheck
npm run build
cd ..

dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

Проверка чистой сборки (важно):

1. Возьмите пустую папку пользователя, запустите `publish\RoManager.exe`.
2. Создайте новый ключ — должен создаться **v2** (`AccountData.json` с `"version": 2`).
3. Добавьте тестовый аккаунт, перезапустите, войдите снова.
4. Убедитесь, что в Release не открываются DevTools (F12) и внешние ссылки.
5. Проверьте, что рядом с `publish` нет `rom-key.json`, `AccountData.json`, `rom.log`.

Затем:

```bat
git tag v2.1.0
git push origin v2.1.0
```

Workflow `Release` соберёт zip, `sha256sums.txt` и опубликует GitHub Release.

## После релиза

- Проверьте скачанный архив: распакуйте в чистую папку и запустите.
- Сверьте SHA-256 с `sha256sums.txt`.
- Обновите `NOTES.md`, если менялись формат данных или поведение.
