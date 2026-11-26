ent-ComputerCargoDelivery = терминал доставки
    .desc = Отправляет дронов карго: ящик с площадки летит на точку доставки нужного отдела.
ent-CargoDeliveryComputerCircuitboard = плата терминала доставки
    .desc = Печатная плата для терминала доставки.
ent-CargoDeliveryPad = площадка дрона доставки
    .desc = Поставьте сюда ящик доставки — дрон заберёт его.
ent-CargoDeliveryDrone = Сипуха (МУВ-1)
    .desc = Малогабаритный универсальный разведывательный дрон. Чёрт, как же я обожаю проект «Атомное сердце»!
ent-CrateCargoDelivery = ящик доставки
    .desc = Защищённый ящик для дрон-доставки. После отправки открыть его может только отдел-получатель.
ent-CargoDeliveryPointBase = точка доставки
    .desc = Размеченная посадочная плита для дронов доставки карго.
ent-CargoDeliveryPointMedical = точка доставки медотдела
    .desc = Сюда дроны карго сбрасывают грузы для медотдела. Ящик открывается медицинским доступом.
ent-CargoDeliveryPointEngineering = точка доставки инженерного отдела
    .desc = Сюда дроны карго сбрасывают грузы для инженеров. Ящик открывается инженерным доступом.
ent-CargoDeliveryPointScience = точка доставки НИО
    .desc = Сюда дроны карго сбрасывают грузы для научного отдела. Ящик открывается доступом НИО.
ent-CargoDeliveryPointSecurity = точка доставки КОВАК
    .desc = Сюда дроны карго доставляют грузы для КОВАК. Ящик открывается доступом отдела безопасности.
ent-CargoDeliveryPointService = точка доставки сервисного отдела
    .desc = Сюда дроны карго доставляют грузы для сервисного отдела. Ящик открывается сервисным доступом.

cargo-delivery-terminal-title = Терминал доставки
cargo-delivery-terminal-send = Отправить дрона
cargo-delivery-terminal-print = Напечатать ящик ({$price} кр.)
cargo-delivery-terminal-print-cooldown = Печать ящика ({$seconds} с)
cargo-delivery-terminal-recall = Отозвать
cargo-delivery-terminal-no-drones = Рядом с площадкой нет дронов.

cargo-delivery-ui-points = Точки доставки
cargo-delivery-ui-no-points = В комплексе нет точек доставки.
cargo-delivery-ui-point-ready = готова • {$distance} м
cargo-delivery-ui-point-cooldown = ждать {$seconds} с
cargo-delivery-ui-point-unavailable = недоступна
cargo-delivery-ui-point-incoming = летит дронов: {$count}
cargo-delivery-ui-contents = Содержимое ящика:
cargo-delivery-ui-contents-empty = — пусто —
cargo-delivery-ui-route = Маршрут:
cargo-delivery-ui-route-select = выберите точку слева
cargo-delivery-ui-route-value = {$point} • {$distance} м
cargo-delivery-ui-fleet = Флот дронов
cargo-delivery-ui-integrity = Целостность: {$value}%

cargo-delivery-pad-status-no-pad = площадка не найдена рядом с терминалом
cargo-delivery-pad-status-no-crate = на площадке нет ящика доставки
cargo-delivery-pad-status-open = ящик открыт — закройте его
cargo-delivery-pad-status-empty = ящик пуст
cargo-delivery-pad-status-mob = в ящике живое существо — дроны не перевозят живых
cargo-delivery-pad-status-forbidden = в ящике запрещённый к перевозке предмет
cargo-delivery-pad-status-reserved = ящик уже ждёт своего дрона
cargo-delivery-pad-status-ready = ящик готов к отправке

cargo-delivery-drone-state-idle = свободен, патрулирует
cargo-delivery-drone-state-to-pad = летит за грузом → {$point}
cargo-delivery-drone-state-loading = погрузка → {$point}
cargo-delivery-drone-state-delivering = везёт груз → {$point}
cargo-delivery-drone-state-returning = возвращается домой
cargo-delivery-drone-state-downed = СБИТ, перезагрузка через {$seconds} с

cargo-delivery-error-access = Доступ запрещён.
cargo-delivery-error-no-drones = Нет свободных дронов.
cargo-delivery-error-no-station = Терминал не подключён к станции.
cargo-delivery-error-no-point = Точка доставки не найдена.
cargo-delivery-error-point-unavailable = Точка доставки недоступна (не закреплена или на другой станции).
cargo-delivery-error-cooldown = На эту точку недавно уже отправляли дрона.
cargo-delivery-error-too-close = Точка слишком близко — отнесите ящик руками.
cargo-delivery-error-print-cooldown = Принтер ящиков ещё перезаряжается.
cargo-delivery-error-pad-occupied = Площадка занята.
cargo-delivery-error-no-money = Недостаточно средств.
cargo-delivery-error-cant-recall = Этого дрона уже нельзя отозвать.

cargo-delivery-abort-no-pad = площадка пропала
cargo-delivery-abort-crate-missing = ящик убрали с площадки
cargo-delivery-abort-point-gone = точка доставки пропала
cargo-delivery-abort-point-moved = точку доставки открутили
cargo-delivery-abort-timeout = истекло время доставки
cargo-delivery-abort-recalled = отозван карго

cargo-delivery-radio-dispatched = {$drone} вылетел за грузом для точки «{$point}».
cargo-delivery-radio-delivered = Груз доставлен на точку «{$point}». Карго начислено {$reward} кр.
cargo-delivery-radio-aborted = Доставка на точку «{$point}» отменена: {$reason}.
cargo-delivery-radio-downed = ВНИМАНИЕ! Дрон с грузом для «{$point}» сбит {$location}! Груз выпал. Карго оштрафовано на {$penalty} кр.
cargo-delivery-radio-returned-crate = Груз для «{$point}» возвращён на площадку.
cargo-delivery-radio-department-arrived = Дрон карго сбросил посылку на точке доставки {$location}. Ящик открывается доступом отдела.

cargo-delivery-point-arrived = Дрон сбрасывает посылку!
cargo-delivery-drone-downed = Дрон искрит и падает!
cargo-delivery-drone-reboot = Дрон перезагружается и взлетает.

cargo-delivery-lock-denied = Ящик заперт для отдела: {$name}.
cargo-delivery-lock-examine = На замке горит адрес: [color=yellow]{$name}[/color].
