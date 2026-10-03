hire-terminal-title = Терминал найма

hire-position-dept-supply = Снабжение
hire-position-cargotechnician = Карготехник
hire-position-cargotechnician-desc = Работа со снабжением и заказами.
hire-position-dept-engineering = Инженерный отдел
hire-position-stationengineer = Инженер
hire-position-stationengineer-desc = Обслуживание систем комплекса.
hire-position-dept-science = Научный отдел
hire-position-scientist = Учёный
hire-position-scientist-desc = Исследования и разработки.
hire-position-roboticist = Робототехник
hire-position-roboticist-desc = Киборги и экзокостюмы.
hire-position-researchassistant = Научный ассистент
hire-position-researchassistant-desc = Помощь учёным.
hire-position-dept-medical = Медицинский отдел
hire-position-medicaldoctor = Врач
hire-position-medicaldoctor-desc = Лечение пациентов.
hire-position-surgeon = Хирург
hire-position-surgeon-desc = Хирургические операции.
hire-position-dept-service = Сервисный отдел
hire-position-bartender = Бартендер
hire-position-bartender-desc = Бар и напитки.
hire-position-chef = Повар
hire-position-chef-desc = Кухня.
hire-position-botanist = Ботаник
hire-position-botanist-desc = Гидропоника.
hire-position-janitor = Уборщик
hire-position-janitor-desc = Чистота в комплексе.

hire-terminal-log-auto = Автоматически
hire-terminal-popup-already-has = У этого паспорта уже есть дополнительная должность.
hire-terminal-popup-no-position = Такой должности не существует.
hire-terminal-popup-already-pending = По этому паспорту уже есть заявка.
hire-terminal-speech-submitted = Заявка отправлена руководителю на проверку. Подтверждение может поступить в любое время — пожалуйста, следите за статусом в терминале.
hire-terminal-popup-no-application = Нет одобренной заявки.
hire-terminal-popup-still-pending = Заявка ещё не рассмотрена руководителем.
hire-terminal-popup-approved = Заявка { $name } одобрена.
hire-terminal-popup-rejected = Заявка { $name } отклонена.
hire-terminal-popup-not-leader = Только руководитель комплекса может рассматривать заявки.
hire-terminal-popup-issued = Выдан ключ: { $position }.
hire-terminal-popup-no-passport = Вставьте паспорт.
hire-terminal-popup-unbound = Паспорт не оформлен.
hire-terminal-popup-not-owner = Это не ваш паспорт.
hire-terminal-note-unbound = Паспорт ещё не оформлен.
hire-terminal-note-approved-by = Одобрил: { $name }
hire-terminal-note-rejected-by = Отклонил: { $name }.
hire-terminal-note-no-leader = Руководителя комплекса нет — ключ выдаётся без проверки.

passport-additional-job-label = Доп. должность
passport-examine-additional-job = Дополнительная должность: [color=#E0B341]{ $job }[/color].

# Terminal UI
hire-ui-log-title = Журнал выдачи
hire-ui-log-subtitle = Кто и какую доп. должность уже получил
hire-ui-log-empty = Пока никто не получал дополнительных должностей.
hire-ui-log-position = { $position } · { $department }
hire-ui-log-meta = { $time } · { $by }

hire-ui-mode-self = Самообслуживание: руководителя комплекса нет, ключ выдаётся сразу после заявки
hire-ui-mode-review = Требуется подтверждение: каждую заявку проверяет руководитель комплекса

hire-ui-holder-caption = Паспорт в приёмнике
hire-ui-holder-job = Должность: { $job }
hire-ui-holder-extra = Доп. должность: { $job }
hire-ui-holder-number = № { $number }
hire-ui-no-passport-hint = Вставьте свой паспорт в приёмник терминала, чтобы подать заявку на дополнительную должность.
hire-ui-eject = Извлечь паспорт

hire-ui-step-position = 1. Выберите дополнительную должность
hire-ui-step-comment = 2. Комментарий к заявке (необязательно)
hire-ui-comment-placeholder = Почему вы хотите получить эту должность?
hire-ui-selected-none = Должность не выбрана
hire-ui-selected = Выбрано: { $position }
hire-ui-rejected-hint = Заявка на «{ $position }» отклонена. { $note } Можно подать новую.
hire-ui-submit-self = Подать заявку и получить ключ
hire-ui-submit-review = Отправить заявку руководителю
hire-ui-claim = Получить ключ доступа

hire-ui-status-none-title = Ожидание паспорта
hire-ui-status-none-text = Вставьте паспорт в приёмник. Подавать заявку может только его владелец.
hire-ui-status-invalid-title = Паспорт не подходит
hire-ui-status-pending-title = Заявка отправлена
hire-ui-status-pending-text = Должность: { $position }
    Ожидайте решения руководителя комплекса. Паспорт можно забрать: вернитесь после одобрения и получите ключ.
hire-ui-status-approved-title = Заявка одобрена
hire-ui-status-approved-text = Должность: { $position }
    { $note }
hire-ui-status-has-title = Доп. должность уже получена
hire-ui-status-has-text = { $job }
    Одному человеку доступна только одна дополнительная должность.

hire-ui-queue-title = Заявки на рассмотрении ({ $count })
hire-ui-queue-title-reviewer = Заявки на рассмотрении ({ $count }): решение за вами
hire-ui-queue-empty = Очередь пуста
hire-ui-queue-applicant = { $name } · { $time }
hire-ui-queue-line = { $job } → { $position }
hire-ui-queue-waiting = Ждёт руководителя
hire-ui-approve = Одобрить
hire-ui-reject = Отклонить
