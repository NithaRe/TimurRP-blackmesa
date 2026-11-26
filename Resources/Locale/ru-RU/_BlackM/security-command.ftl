security-command-window-title = Тактическое командование СБ
security-command-viewer-title = Карта задач СБ
security-command-new-order = Новый приказ
security-command-target = Кому
security-command-text = Текст:
security-command-text-placeholder = Текст приказа
security-command-color = Цвет:
security-command-finish = Завершить
security-command-cancel = Отмена

security-command-tab-officers = Сотрудники
security-command-tab-squads = Отряды
security-command-tab-orders = Приказы

security-command-type-move = Выдвинуться
security-command-type-move-desc = Отметить точку, куда нужно прибыть. Сотрудники увидят метку и направляющую стрелку.
security-command-type-post = Пост / КПП
security-command-tool-move = Точка
security-command-tool-post = КПП
security-command-tool-route = Маршрут
security-command-tool-zone = Зона
security-command-type-post-desc = Обведите КПП двумя кликами по противоположным углам. Время дежурства можно указать в поле таймера.
security-command-post-timer-input = Таймер КПП, мин
security-command-post-timer-help = Укажите, сколько минут сотрудник должен оставаться на КПП. 0 — без таймера.
security-command-post-duration = Дежурство на КПП: {$minutes} мин
security-command-post-waiting = {$minutes} мин после прибытия
security-command-post-countdown = На КПП осталось {$time}
security-command-post-completed = Дежурство на КПП завершено
security-command-post-left-early = Дежурство на КПП прервано
security-command-post-left-early-notice = {$officer} не завершил дежурство на посту «{$post}». Оставалось {$time}.
security-command-post-invalid-area = Укажите два противоположных угла, чтобы задать зону КПП с таймером.
security-command-type-route = Маршрут патруля
security-command-type-route-desc = Расставьте точки маршрута ЛКМ, ПКМ — завершить. Патруль идёт по кругу.
security-command-type-zone = Зона патруля
security-command-type-zone-desc = Укажите два противоположных угла зоны, которую нужно патрулировать.
security-command-type-danger = Опасность
security-command-type-danger-desc = Отметить опасное место: засаду, нарушителей, аномалию.
security-command-type-label = Надпись
security-command-type-label-desc = Оставить надпись на местности. Требуется текст.

security-command-color-red = Красный
security-command-color-orange = Оранжевый
security-command-color-yellow = Жёлтый
security-command-color-green = Зелёный
security-command-color-cyan = Голубой
security-command-color-blue = Синий
security-command-color-purple = Фиолетовый
security-command-color-white = Белый

security-command-target-all = Всем сотрудникам
security-command-target-squad = Отряд «{$name}»
security-command-target-officer = Лично: {$name}
security-command-target-personal = Лично вам
security-command-target-unknown = Неизвестно

security-command-hint-point = ЛКМ — поставить, ПКМ — отмена
security-command-hint-route = Точек {$count}/{$max} · ПКМ — завершить
security-command-hint-zone = Зона: {$count ->
    [0] кликните первый угол.
   *[other] кликните противоположный угол.
}
security-command-hint-route-need-points = Для маршрута нужно минимум 2 точки.
security-command-hint-need-text = Для надписи сначала введите текст.

security-command-cursor-point = ЛКМ: {$type} · ПКМ: отмена
security-command-cursor-route = Точка {$count}/{$max} · ПКМ: завершить
security-command-cursor-zone-first = ЛКМ: первый угол зоны
security-command-cursor-zone-second = ЛКМ: второй угол зоны

security-command-officers-empty = Нет сотрудников СБ на смене.
security-command-officer-commander = [КОМ] {$name}
security-command-officer-order = Приказ
security-command-officer-order-desc = Выбрать этого сотрудника целью следующего приказа.
security-command-status-alive = В строю
security-command-status-critical = Ранен
security-command-status-dead = Погиб
security-command-status-offline = Не на связи

security-command-squad-name-placeholder = Название отряда
security-command-squad-create = Создать отряд
security-command-squad-none = Без отряда
security-command-squad-default-name = Отряд {$number}
security-command-squads-empty = Отрядов нет. Создайте отряд и распределите сотрудников во вкладке «Сотрудники».
security-command-squad-members = Бойцов: {$count}
security-command-squad-order = Приказ отряду
security-command-squad-remove = Расформировать

security-command-orders-empty = Активных приказов нет.
security-command-orders-clear = Отменить все приказы
security-command-order-age = {$minutes} мин назад
security-command-order-issuer = Отдал: {$name}
security-command-order-remove = Отменить

security-command-hud-title = ПРИКАЗЫ СБ
security-command-hud-squad = Отряд: {$name}
security-command-hud-more = ...и ещё {$count}
security-command-dist = {$meters} м
security-command-dist-arrived = НА МЕСТЕ
security-command-dist-in-zone = В ЗОНЕ
security-command-dist-on-route = НА МАРШРУТЕ
security-command-dist-in-danger = ОПАСНО!
security-command-dist-other-level = другой уровень

security-command-popup-incapacitated = Вы не в состоянии командовать.
security-command-popup-too-many-orders = Слишком много активных приказов. Отмените старые.
security-command-popup-too-many-squads = Достигнут лимит отрядов.
security-command-popup-need-text = Для надписи нужен текст.
security-command-popup-new-order = Новый приказ: {$type}. {$text}
security-command-popup-new-order-notext = Новый приказ: {$type}.
security-command-popup-order-cancelled = Приказ отменён: {$type}.
security-command-popup-orders-cleared = Все приказы отменены.
security-command-popup-assigned = Вы назначены в отряд «{$squad}».
security-command-popup-unassigned = Вы выведены из отряда.
security-command-popup-squad-disbanded = Отряд «{$squad}» расформирован.

security-command-map-title = Тактическая карта комплекса
security-command-map-grid-none = Нет навкарты
security-command-map-refresh = Обновить уровни
security-command-map-center-me = К себе
security-command-map-hint = ПКМ по карте — меню приказов · ЛКМ по бойцу — отряды и приказы · колесо — масштаб · перетаскивание — сдвиг
security-command-map-no-navmap = Навкарта недоступна: у этого уровня нет данных навигации.
security-command-map-placing = Расстановка: {$type} — кликните по карте
security-command-legend-officer = Боец
security-command-legend-commander = Командир
security-command-legend-critical = Крит
security-command-legend-dead = Погиб
security-command-legend-offline = Не в сети
security-command-order-show = Показать
security-command-order-show-desc = Показать приказ на тактической карте.
security-command-officer-show-desc = Показать сотрудника на тактической карте.

security-command-sidebar-toggle = Бойцы, отряды, приказы
security-command-toolbar-collapse = Свернуть панель
security-command-map-reached = ✓ Вы уже на месте
security-command-map-officer-hint = ЛКМ — отряды и приказы
security-command-menu-show = Показать на карте
security-command-menu-cancel-order = Отменить приказ
security-command-menu-order-personal = Приказ лично
security-command-menu-order-squad = Приказ отряду «{$name}»
security-command-menu-squad = Отряд:
security-command-menu-order-here = Приказ сюда
security-command-menu-target = Кому: {$target}
security-command-menu-start-from = {$type} — начать отсюда
security-command-menu-clear-all = Отменить все приказы

security-command-hint-post = КПП: {$count ->
    [0] кликните первый угол.
   *[other] кликните противоположный угол.
}
security-command-cursor-post-first = ЛКМ: первый угол КПП
security-command-cursor-post-second = ЛКМ: второй угол КПП
security-command-dist-on-post = НА КПП

security-command-map-level = Уровень комплекса
security-command-officers-help = Нажмите на сотрудника, чтобы выбрать отряд.
security-command-squad-new = Новый отряд
security-command-tab-count = {$name} · {$count}
security-command-officer-commander-tag = КОМ
security-command-squad-members-none = В отряде пока никого нет
