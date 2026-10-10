cmd-phrasewheel-desc = Выдать или забрать доступ к меню фраз
cmd-phrasewheel-help =
    Использование: phrasewheel <ник|all|revokeall> [категория...]
    <ник> без категорий — выдать доступ ко всем фразам (или забрать, если доступ уже есть).
    <ник> с категориями — выдать или обновить доступ только к ним.
    all [категория...] — выдать доступ всем игрокам, (без категорий — ко всем фразам).
    revokeall — забрать доступ у всех игроков
cmd-phrasewheel-hint-target = <ник|all|revokeall>
cmd-phrasewheel-hint-category = [категория]
cmd-phrasewheel-completion-all = Выдать всем
cmd-phrasewheel-completion-revokeall = Забрать у всех

cmd-phrasewheel-error-player-not-found = Игрок '{ $name }' не найден или не управляет сущностью.
cmd-phrasewheel-error-unknown-category = Неизвестная категория '{ $category }'.

cmd-phrasewheel-revoked = Доступ к меню фраз забран у { $name }.
cmd-phrasewheel-updated = Категории [{ $categories }] обновлены у { $name }.
cmd-phrasewheel-granted-full = { $name } выданы все фразы.
cmd-phrasewheel-granted-categories = { $name } выдан доступ к категориям [{ $categories }].
cmd-phrasewheel-all-done-full = Все фразы выданы игрокам: { $count }
cmd-phrasewheel-all-done-categories = Категории [{ $categories }] выданы игрокам: { $count }
cmd-phrasewheel-revokeall-done = Доступ забран у игроков: { $count }

phrase-wheel-recent = Недавние
phrase-wheel-empty = Пока пусто
phrase-wheel-close = Закрыть
phrase-wheel-page = стр. { $page }/{ $pages } (колесо мыши)
phrase-wheel-cooldown = ждите { $seconds } с
phrase-wheel-hint-whisper = шёпот
phrase-wheel-hint-emote = действие
phrase-wheel-hint-shout = крик
phrase-wheel-color-label = Цвет текста:
phrase-wheel-color-reset = Сбросить цвет

phrase-wheel-category-global = Общие
phrase-wheel-category-hecu = HECU
phrase-wheel-category-kaiflife = KaifLife
phrase-wheel-category-meme = Мемы
