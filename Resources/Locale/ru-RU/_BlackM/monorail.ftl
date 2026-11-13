monorail-car-departure-warning = Вагон отправляется через { $seconds } сек. Займите места и держитесь за поручни!
monorail-car-doors-closing = Осторожно, двери закрываются!
monorail-car-arrived = Вагон прибыл на станцию.
monorail-car-arrived-named = Станция «{ $name }».
monorail-car-examine-departs = Отправление через [color=yellow]{ $seconds }[/color] сек.
monorail-car-examine-moving = Вагон [color=lightgreen]в пути[/color].
monorail-car-examine-next = Следующая станция: [color=violet]{ $name }[/color].
monorail-car-examine-passengers = Пассажиров: { $count }. Сидячих мест: { $seats }, остальные едут стоя.
monorail-car-examine-no-route = [color=red]Маршрут не найден.[/color] Нужна трасса рядом и минимум две станции (или одна на кольце).
monorail-seat-board-moving = Двери закрыты — сесть с платформы не получится!
monorail-seat-unbuckle-moving = Нельзя выйти на ходу!

ent-BlackMMonorailTrackBase = монорельс
    .desc = Стальная балка монорельса на опорных кронштейнах.
ent-BlackMMonorailTrack = монорельс
    .desc = Стальная балка монорельса на опорных кронштейнах.
ent-BlackMMonorailTrackLong = длинный монорельс
    .desc = Стальная балка монорельса на три тайла.
ent-BlackMMonorailTrackCorner = поворот монорельса
    .desc = Крутой поворот. Длинный вагон срезает его по хорде — лучше ставить широкий.
ent-BlackMMonorailTrackCurveLarge = широкий поворот монорельса
    .desc = Широкий поворот 3x3 для полноразмерных вагонов.
ent-BlackMMonorailTrackEnd = тупик монорельса
    .desc = Конец пути с гидравлическим буфером.
ent-BlackMMonorailTrackStation = станция монорельса
    .desc = Участок пути с меткой остановки. Здесь вагон останавливается.
ent-BlackMMonorailCar = вагон BMRF Transit
    .desc = Транспортный вагон комплекса «Чёрная Меза». Сам ездит между станциями. Садитесь или держитесь за поручень.
ent-BlackMMonorailSeat = сиденье вагона
    .desc = Мягкое сиденье на стальной раме.

cmd-monorailsnap-desc = Шаг привязки и магнит при установке пути монорельса.
cmd-monorailsnap-help = Использование: {$command} [tile|half|quarter|free] [magnet|nomagnet]
    tile — центры тайлов, half — шаг 0.5, quarter — шаг 0.25, free — без сетки.
    magnet / nomagnet — притягивать ли к свободным концам пути. Без аргументов — текущие настройки.
cmd-monorailsnap-status = Монорельс: шаг { $step }, магнит { $magnet }.
cmd-monorailsnap-error = Неизвестный параметр: { $arg }. Можно: tile, half, quarter, free, magnet, nomagnet.
cmd-monorailsnap-hint = шаг или магнит

monorail-verb-category = Монорельс
monorail-verb-align-grid = Выровнять по сетке
monorail-verb-align-grid-done = Кусок выровнен по сетке.
monorail-verb-snap = Притянуть к пути
monorail-verb-snap-done = Кусок пристыкован.
monorail-verb-snap-fail = Рядом нет свободного конца пути.
monorail-verb-align-all = Выровнять весь путь на гриде
monorail-verb-align-all-done = Выровнено кусков: { $count }.

monorail-car-hit = Вагон сбивает { $target }!
monorail-car-shove = Вагон отталкивает { $target }.

monorail-board-arriving-in = ЧЕРЕЗ
monorail-board-arriving-now = ПРИБЫВАЕТ
monorail-board-boarding = ПОСАДКА
monorail-board-doors = ДВЕРИ
monorail-board-closing = ЗАКРЫВАЮТСЯ
monorail-board-offline = НЕТ СВЯЗИ
monorail-board-examine-no-stop = [color=red]Рядом нет станции монорельса.[/color] Поставьте табло ближе к станции.
monorail-board-examine-stop = Табло подключено к станции.
monorail-board-examine-stop-named = Табло станции «[color=orange]{ $name }[/color]».

ent-BlackMMonorailArrivalBoardBase = табло прибытия
    .desc = Светодиодное табло. Показывает, через сколько прибудет вагон.
ent-BlackMMonorailArrivalBoard = табло прибытия монорельса
    .desc = Светодиодное табло на стойке. Показывает, через сколько прибудет вагон.
ent-BlackMMonorailArrivalBoardWall = настенное табло прибытия
    .desc = Светодиодное табло на стене. Показывает, через сколько прибудет вагон.
