engineering-breakdown-sender = технической службы комплекса
engineering-breakdown-location-unknown = неустановленный участок

station-event-engineering-breakdown-start-announcement = Внимание, персонал комплекса. Системы мониторинга фиксируют скачки напряжения в распределительной сети. Возможны локальные отказы оборудования. Не прикасайтесь к искрящим и дымящим устройствам.
station-event-engineering-breakdown-cascade-start-announcement = ТРЕВОГА. Зафиксирован каскадный отказ энергосистемы комплекса. Всему инженерному персоналу немедленно прибыть к местам аварий. Остальным сотрудникам покинуть технические помещения.

engineering-breakdown-announcement-entry = {"  "}▸ { $name } — [bold]{ $location }[/bold]
engineering-breakdown-announcement-targets = [bold]НАРЯД НА АВАРИЙНЫЙ РЕМОНТ[/bold]
    Системы мониторинга зафиксировали отказы оборудования:{ $list }
    Инженерному отделу — немедленно приступить к устранению.
    Персоналу — не приближаться к повреждённым узлам до прибытия ремонтной бригады.
engineering-breakdown-announcement-reminder = [bold]ПОВТОРНОЕ ОПОВЕЩЕНИЕ[/bold]
    Следующие неисправности всё ещё не устранены:{ $list }
    Нагрузка на резервные линии растёт. Промедление грозит аварией.
engineering-breakdown-announcement-progress = [bold]Узел восстановлен[/bold] — { $location }. Телеметрия в норме, отличная работа.
engineering-breakdown-announcement-success = [bold]РЕМОНТ ЗАВЕРШЁН[/bold]
    Все зафиксированные неисправности устранены, системы комплекса работают в штатном режиме.
    Администрация выражает благодарность инженерному отделу.
engineering-breakdown-announcement-reward = По распоряжению администрации на счёт отдела снабжения перечислена премия: [bold]{ $amount } кр.[/bold]
engineering-breakdown-announcement-reward-partial = За устранённые неисправности на счёт отдела снабжения перечислено: [bold]{ $amount } кр.[/bold]
engineering-breakdown-announcement-failure = [bold]АВАРИЙНОЕ ОПОВЕЩЕНИЕ[/bold]
    { $escalate ->
        [true] Неустранённые неисправности перешли в аварийный режим:{ $list }
       *[false] Ремонтные работы не завершены, повреждённые узлы отключены защитой:{ $list }
    }
    Персоналу держаться подальше от поражённых участков.

engineering-breakdown-verb-repair = Устранить неисправность
engineering-breakdown-wrong-tool = Этим не починить. Нужно { $hint }.
engineering-breakdown-already-repairing = Устройство уже кто-то ремонтирует.
engineering-breakdown-busy-self = Вы уже этим заняты.
engineering-breakdown-shocked = Искрящая проводка бьёт вас током!
engineering-breakdown-repaired = {CAPITALIZE(THE($device))} снова работает исправно.
engineering-breakdown-examine = [color={ $color }][bold]{ $name }.[/bold][/color] { $desc }
engineering-breakdown-examine-step = Похоже, нужно [color=yellow]{ $hint }[/color].

engineering-breakdown-apc-overload-name = Перегрузка ЛКП
engineering-breakdown-apc-overload-desc = Выбит главный автомат, контакты оплавлены и искрят.
engineering-breakdown-light-short-name = Замыкание освещения
engineering-breakdown-light-short-desc = Пробой балласта, лампа мигает.
engineering-breakdown-light-burnout-name = Выгоревший светильник
engineering-breakdown-light-burnout-desc = Проводка сгорела, светильник обесточен и дымит.
engineering-breakdown-vent-rupture-name = Разрыв вентиляции
engineering-breakdown-vent-rupture-desc = Трещина в корпусе, из неё бьёт пар.
engineering-breakdown-sensor-fault-name = Сбой датчика
engineering-breakdown-sensor-fault-desc = Контроллер завис после скачка напряжения.
engineering-breakdown-airlock-jam-name = Заклинивший шлюз
engineering-breakdown-airlock-jam-desc = Сервопривод сгорел, болты заблокированы.
engineering-breakdown-vending-short-name = Замыкание автомата
engineering-breakdown-vending-short-desc = Плата питания сгорела.
engineering-breakdown-power-node-name = Пробой энергоузла
engineering-breakdown-power-node-desc = Пробита изоляция шин, видна электрическая дуга. Опасно!
engineering-breakdown-hint-apc-panel = снимите оплавленную панель отвёрткой
engineering-breakdown-done-apc-panel = Панель снята
engineering-breakdown-hint-cut-burnt = срежьте обгоревшие провода кусачками
engineering-breakdown-done-cut-burnt = Обгоревшие провода удалены
engineering-breakdown-hint-calibrate = откалибруйте контроллер мультитулом
engineering-breakdown-done-calibrate = Контроллер откалиброван
engineering-breakdown-hint-light-cover = закрепите плафон и контакты отвёрткой
engineering-breakdown-done-light-cover = Контакты закреплены
engineering-breakdown-hint-light-ballast = прозвоните балласт мультитулом
engineering-breakdown-done-light-ballast = Балласт исправлен
engineering-breakdown-hint-light-pry = снимите оплавленный плафон ломом
engineering-breakdown-done-light-pry = Плафон снят
engineering-breakdown-hint-weld-crack = заварите трещину сваркой
engineering-breakdown-done-weld-crack = Трещина заварена
engineering-breakdown-hint-tighten = затяните крепления гаечным ключом
engineering-breakdown-done-tighten = Крепления затянуты
engineering-breakdown-hint-sensor-open = вскройте корпус датчика отвёрткой
engineering-breakdown-done-sensor-open = Корпус вскрыт
engineering-breakdown-hint-sensor-flash = перепрошейте контроллер мультитулом
engineering-breakdown-done-sensor-flash = Контроллер перепрошит
engineering-breakdown-hint-airlock-panel = откройте сервисную панель отвёрткой
engineering-breakdown-done-airlock-panel = Панель открыта
engineering-breakdown-hint-airlock-servo = перезапустите сервопривод мультитулом
engineering-breakdown-done-airlock-servo = Сервопривод перезапущен
engineering-breakdown-hint-node-pry = вскройте кожух ломом
engineering-breakdown-done-node-pry = Кожух вскрыт
engineering-breakdown-hint-node-weld = наварите новую шину сваркой
engineering-breakdown-done-node-weld = Шина восстановлена
