# Drag-Drop подсистема — дизайн и дорожная карта

Цель: дать движку **простой, легко подключаемый** drag-drop, который (а) работает через
**вьюмодель** (payload — данные, доставка в `ICommand`), а не только на уровне контролов; (б)
**ретрофитится на любой существующий контрол** без правки его кода («подключить там, где забыли»);
(в) масштабируется от внутри-оконного к кросс-оконному в пределах аппки и далее к интеропу с ОС.

Это **не** клон WPF OLE-модели. WPF гонит вообще всё (даже внутри-оконное) через блокирующий
`DoDragDrop` + `IDataObject`, теряя быстрый путь с живым объектом. Мы берём суть (данные + таргетинг
+ события), а тяжёлый OLE подключаем только там, где он реально нужен — на границе с ОС.

---

## Три уровня (их НЕЛЬЗЯ мешать в один механизм)

| Уровень | Транспорт | Payload | Когда |
|---|---|---|---|
| 1. Внутри-оконный | наш loop (capture + hit-test + routed + ICommand) | живой CLR-объект | базовый случай |
| 2. Кросс-оконный, ОДНА аппка | тот же loop, но app-global сессия + hit-test по экрану через все окна | живой CLR-объект (без сериализации) | доки, перенос между панелями/окнами |
| 3. Аппка ↔ ОС (Explorer и др.) | Win32 **OLE** (`IDropTarget` / `DoDragDrop` / `IDataObject`) | стандартные форматы (файлы/текст/байты) | интероп с чужими процессами |

Уровни 1–2 — единый наш движок. Уровень 3 — отдельная платформенная подсистема со своим циклом,
которая **бриджит к тому же публичному API** через абстракцию данных (ниже).

---

## Что уже есть в движке (на это опираемся, ничего параллельного не строим)

- **Захват мыши:** `Mouse.Capture` / `MouseDevice` (app-global синглтон); `SyncOsMouseCapture` уже
  зеркалит захват в ОС на уровне window-worker — захватившее окно продолжает получать move/up, даже
  когда курсор ушёл за его пределы (несущее для уровня 2).
- **Hit-test под курсором:** `MouseDevice.HitTestTopmost` / `InputExtensions.HitTest` — умеют найти
  элемент под точкой даже при захвате (адресация drop-таргета).
- **Routed events:** `EventManager.RegisterRoutedEvent` (Bubble/Tunnel), `RegisterClassHandler<T>` —
  по ним пустим `DragEnter/DragOver/DragLeave/Drop`. Эталон порогового drag — `Thumb`.
- **Подключение без правки контрола:** `Behavior<T>` + `BehaviorCollection` и attached-свойства
  (`AdamantiumProperty.RegisterAttached`, эталон — `Canvas.Left/Top`).
- **Слепок визуала (для призрака):** `RenderTargetImage : BitmapSource : ImageSource` (ImageSource за
  render target'ом) + `RenderTargetGraphicsPresenter` (рендер в RT, а не свопчейн) + живой
  texture-draw путь `DrawImage` (`RenderUnit.cs:737`: «RenderTargetImage also render»). **Полноценный
  `VisualBrush`-как-кисть (TexRectData/pass TexRect/bindless из TILE_BRUSH_PLAN) для призрака НЕ нужен.**
- **Окно-призрак:** `WindowStyleEx` содержит `Layered` / `Transparent` / `Topmost` / `Noactivate`;
  `Win32NativeWindowWrapper.CreateWindowExW` принимает ex-стиль.
  *(Историческая заметка: раньше на обычных окнах стоял `Acceptfiles` — частичный WM_DROPFILES. Он УБРАН
  в Фазе 5, вместо него полноценный OLE-drop-таргет.)*

---

## Зафиксированные решения

1. **Призрак = РЕАЛЬНОЕ top-level окно**, а не внутри-оконный overlay. Причина: он обязан (а) выходить
   за границы своего окна (перетаскивание наружу даже внутри нашей аппки) и (б) быть всегда поверх и
   **не обрезаться** ничьим клип-ректом. Стили: `Layered | Transparent | Topmost | Noactivate`
   (+ `Toolwindow`, чтобы не светиться в таскбаре/Alt-Tab), click-through.
2. **Призрак = статичный bitmap на прозрачном окне, композит делает ОС (кросс-платформенно одинаково).**
   *Точность (не догма):* со свопчейном несовместим именно `UpdateLayeredWindow` (per-pixel альфа через
   GDI-DIB); `SetLayeredWindowAttributes` (colorkey/константная альфа) со свопчейном работает, но не даёт
   мягких краёв; per-pixel-альфа + живой Vulkan возможны через DirectComposition, которого в аппке нет.
   Главное окно, к слову, вообще НЕ layered — оно `Appwindow|Acceptfiles` + DWM
   (`DwmExtendFrameIntoClientArea`).
   Призрак статичен во время drag → берём самый простой и СИММЕТРИЧНЫЙ путь: печём элемент в
   `RenderTargetImage` один раз → **readback в CPU RGBA-bitmap** (платформенно-нейтрально, Vulkan) → отдаём
   bitmap платформенному окну-призраку. Единый контракт `IDragGhost { Show(bitmap,pos); Move(pos); Hide() }`,
   две мелкие реализации над ОДНИМ bitmap:
   - **Windows:** layered-окно + `UpdateLayeredWindow`; позиция за курсором через `SetWindowPos`.
   - **macOS:** прозрачный borderless `NSWindow` (`opaque=NO`, clear bg, `ignoresMouseEvents=YES`=click-through,
     floating level=topmost) + `CALayer.contents = CGImage`. Нативная per-pixel альфа, без Metal/Vulkan на окне.

   Обе стороны получают один и тот же bitmap и показывают его в прозрачном floating click-through окне →
   поведение идентично; единственный per-platform нюанс — формат пикселей (BGRA-premultiplied vs CGImage
   bitmap-info). Живой Vulkan-призрак (DComp на Win / CAMetalLayer на Mac) РАЗВёл бы платформы — поэтому
   статичный вариант выбран **в т.ч. ради кросс-платформенной симметрии**.
   *Бонус: ноль новых `.fx`-пассов → никакого драйверного риска `vkCreateShadersEXT`.*
3. **Payload = абстракция `DragData` / `IDataPackage`**, несущая ЛИБО живой CLR-объект (уровни 1–2,
   быстрый путь), ЛИБО именованные форматы (`Text`, `Files`, произвольные байты — уровень 3). Один и
   тот же `DropCommand`/`Drop`-хендлер работает и для нашего списка, и для файла из Explorer:
   `data.Get<MyItem>()` / `data.Contains("Files")`. Закладываем СРАЗУ, иначе публичный API придётся
   ломать под OLE.
4. **Доставка в VM = `ICommand` (первично) + routed события.** `DropCommand`/`DragOverCommand` с
   параметром `DragEventArgs { Data, Source, Position, Effects }` — VM реагирует командой, **никаких
   UI-типов в VM**. Routed `Drop/DragEnter/DragOver/DragLeave` — для контрол-уровня и триггеров
   (подсветка `IsDragOver`).
5. **Подключение = attached-свойства (первично, ретрофит) + Behaviors (обёртки).** Явные bindable-гейты
   разрешения: `DragDrop.AllowDrag` (источник) и `DragDrop.AllowDrop` (таргет) — `bool`, дефолт `false`;
   флипаются в рантайме (заблокированный айтем → `AllowDrag=false`; readonly-зона → `AllowDrop=false`), в
   т.ч. через `{Binding}`.
   ```xml
   <Border DragDrop.AllowDrag="True" DragDrop.DragData="{Binding Item}"/>
   <StackPanel DragDrop.AllowDrop="{Binding CanAcceptDrop}" DragDrop.DropCommand="{Binding DropCmd}"/>
   ```
   **Два уровня разрешения (дополняют друг друга):** статический гейт `AllowDrag`/`AllowDrop` (element-level,
   флип в любой момент) + per-drag динамика `Effects` в `DragOver` («можно ли ИМЕННО этот payload сюда прямо
   сейчас» → курсор ⊘). Первый — «этот элемент вообще участвует», второй — «этот конкретный груз сюда».
6. **Вид жеста выбирается ОДИН раз на старте по объявлению источника — НИКАКОГО мид-драг переключения.**
   Причина: `DoDragDrop` — блокирующий модальный OLE-цикл, он должен стартовать в начале жеста (забирает
   capture + message-loop); «поднять OLE на середине / при пересечении границы окна» технически нечисто и
   привело бы к create/destroy на каждом переходе — так НЕ делаем.
   - **In-app-only источник** (payload = CLR-объект, внешних форматов нет) → наш loop от начала до конца,
     **ноль OLE**. Курсор гуляет через границы окон / над десктопом — призрак (topmost-окно) виден везде,
     app-global loop находит таргет в любом нашем окне; отпустил над чужим приложением → просто отмена.
   - **External-capable источник** (объявил `AllowExternalDrag` / даёт сериализуемые форматы) → **один**
     OLE `DoDragDrop` на весь жест; дропы обратно на наши окна идут через их всегда-включённый `IDropTarget`
     (зарегистрирован один раз при создании окна, НЕ на каждый drag). Пересечение границы туда-сюда = OLE
     шлёт enter/leave на таргетах, **ноль аллокаций**.
   Так мы не платим OLE-налог за 95% случаев (in-app), сохраняем живой CLR-payload, и наш платформенно-
   нейтральный loop одинаков на Win/Mac (OLE — Windows-only; внешний DnD на Mac = NSDragging).
7. **Курсор-фидбэк «можно / нельзя» — из коробки + ручной override.** Первично: таргет в
   `DragEnter/DragOver` ставит `DragEventArgs.Effects` (None/Copy/Move/Link) → менеджер маппит в курсор
   (`None→Cursors.No` ⊘, `Move→Cursors.SizeAll`, `Copy→`copy, …) и ставит `Mouse.OverrideCursor`. `DragOver`
   летит на каждый move → курсор меняется **живьём** при заходе в запретную/разрешённую зону (MVVM: VM
   отвечает «можно ли сюда» эффектом, без UI-типов). Полный контроль в любой момент:
   `DragEventArgs.DragCursor` (settable, перебивает дефолт-мапу) + routed-событие `DragDrop.GiveFeedback`
   на источнике (WPF-стиль) для произвольного курсора. OLE-путь ведёт курсор через
   `IDropSource::GiveFeedback` — тот же `Effects→курсор`. Готовое: `Cursors.No`/`SizeAll`/`Hand`/`Arrow` уже
   есть; `Mouse.OverrideCursor`/`SetCursor` тоже.

---

## Компоненты (публичный API)

_Сверено с кодом 2026-07-27. ✅ = есть в коде, ❌ = НЕТ (изначальный замысел, который не понадобился либо
отложен). Не переписывай этот раздел по памяти — сверяй грепом._

- ✅ **`DragDrop`** (`Adamantium.UI/Input/DragDrop.cs` + `DragDrop.External.cs`, ОДИН статический класс на два
  файла): attached-свойства `AllowDrag`, `DragData`, `AllowDrop`, `DropCommand`, `DragOverCommand`,
  `DragStartedCommand`, `DragCompletedCommand`, `DragTemplate`, `AllowExternalDrag`, `AutoScrollSpeed`,
  `IsDragOver`.
  - ❌ **метод `DragDrop.DoDragDrop(source, data, effects)` для кодового старта — НЕ реализован.** Драг
    начинается только жестом (нажатие + порог). Понадобится для «начать перетаскивание из команды».
  - ✅ **routed-события `DragEnter/DragOver/DragLeave/Drop`** (`Adamantium.UI.Core/Input/DragDropEvents.cs`,
    CLR-обёртки на `IInputComponent`/`InputUIComponent`) — контрол-уровень: свой контрол реагирует на пролёт
    драга БЕЗ вьюмодели. Подробности ниже, в «Routed-события».
  - ❌ **`GiveFeedback` — НЕ реализован** (курсор драга сейчас выбирает движок по `Effects`; ручной override —
    см. `DragCursor` в «Что осталось»).
- ❌ **`DragDropManager` — НЕ существует и НЕ будет.** Решение зафиксировано: сессия живёт прямо в статическом
  `DragDrop` (он и так app-global). Раздел оставлен как история — не заводить.
- ✅ **`DragData` / `DataPackage`** (`Adamantium.UI.Core/Input/`): `Get<T>()`, `Get(format)`, `Set(object)`,
  `Set(format, value)`, `Contains(format)`, `Contains<T>()`, `GetFormats()`.
- ✅ **`IDragGhost`** — Win32-реализация есть (`Win32DragGhost`, layered + `UpdateLayeredWindow`, + DPI-слежение).
  ❌ macOS-реализации нет.
- ✅ **`DragSourceBehavior` / `DropTargetBehavior`** — есть, включая `AllowExternalDrag` у источника.
- **`DragDropEventArgs`** (имя такое, не `DragEventArgs` — тот занят `Thumb`); наследник `RoutedEventArgs`:
  ✅ `Data`, `DragSource`, `Position`, `Effects`, `SourceItemsSource`, `InsertIndex`, `InsertBefore`, `DropTarget`,
  `Placement`, `Handled` (+ `Source`/`OriginalSource` от маршрутизации).
  ❌ `GetPosition(relativeTo)`, ❌ `DragCursor` (settable-переопределение курсора) — НЕ реализованы.

### Routed-события (контрол-уровень)

`Adamantium.UI.Core/Input/DragDropEvents.cs` — `DragEnterEvent`, `DragOverEvent`, `DragLeaveEvent`,
`DropEvent`; CLR-обёртки `DragEnter/DragOver/DragLeave/Drop` объявлены на `IInputComponent` и реализованы в
`InputUIComponent`. Payload — тот же `DragDropEventArgs`, что получают команды.

- **Где поднимаются.** На ближайшем предке с `DragDrop.AllowDrop="True"` (том же элементе, что получает
  `DropCommand`), дальше **bubble** вверх по дереву. `OriginalSource` = самый глубокий элемент под курсором —
  по нему хендлер понимает, над КАКОЙ строкой летит драг; `Source` по ходу маршрута меняется, как у любого
  routed-события.
- **Кто решает `Effects`.** Только `DragOver` — он приходит на каждое движение. `DragEnter`/`DragLeave` —
  уведомления о смене цели (то, что они впишут в `Effects`, не читается: `DragOver` того же прохода перепишет).
- **`Handled` глушит команду.** Хендлер, поставивший `Handled = true`, не только останавливает маршрут, но и
  отменяет соответствующий `DragOverCommand`/`DropCommand`: контрол, разобравшийся с драгом, владеет им
  целиком. Порядок — сначала routed-событие, потом команда.
- **Симметрия Enter/Leave гарантирована.** `DragLeave` приходит и после успешного дропа, и при отмене, и когда
  OS-драг ушёл из окна — тем же сбросом, что гасит `IsDragOver`. Контрол, подсветившийся на `DragEnter`, не
  останется подсвеченным.
- **Один путь для своего и чужого драга.** Внутренний драг и OLE-драг из другого приложения поднимают эти
  события одинаково — хендлер не узнаёт, откуда payload.
- **Почему `DragDropEvents`, а не `DragDrop`.** `RoutedEvent`-поля обязаны лежать в `Adamantium.UI.Core`
  (оттуда их видят CLR-обёртки в `Adamantium.UI.Controls`), а сам `DragDrop` живёт ЭТАЖОМ ВЫШЕ, в
  `Adamantium.UI`, и зависит от Controls. Назвать корный класс `DragDrop` нельзя — два одноимённых типа в
  разных namespace'ах дают CS0104 в файлах, где импортированы оба. Ровно та же схема, что у `Mouse`.
- **Tunnel-варианты (`PreviewDrag*`) НЕ заведены** — по одной строке на каждый, если понадобится veto с
  уровня родителя.

---

## Фазы

_Статус сверен с кодом 2026-07-27._

| Фаза | Статус |
|---|---|
| 0 — `VisualRenderer` (слепок визуала) | ✅ сделана |
| 1 — окно-призрак | ✅ сделана (Windows) |
| 2 — движок in-window | ✅ сделана, включая routed-события (см. «Routed-события (контрол-уровень)») |
| 3 — кросс-оконный в одной аппке | ✅ сделана, включая z-order и подъём окна (ниже) |
| 4 — Behaviors + `DragTemplate` + `IsDragOver` | ✅ сделана |
| 5 — приём из чужих приложений (OLE) | ✅ сделана (Windows) |
| 6 — выдача наружу (OLE) | ✅ сделана (Windows) |

**Фаза 3, как она устроена в итоге (важно, если полезешь править):**

- **Окно под курсором спрашивается у ОС** — `IApplicationPlatform.WindowFromScreenPoint` (Win32:
  `WindowFromPoint`). Перебор `UIApplication.Windows` в порядке коллекции, который был раньше, врал дважды:
  при перекрытии целился не в то окно И считал попаданием случай, когда сверху лежит **чужое** приложение.
  Теперь: сверху не наше окно → цели нет. Призрак click-through, поэтому ОС его не возвращает никогда.
  Где платформа не отвечает (macOS) — фолбэк на попадание в клиентские границы, то есть прежнее поведение.
- **Окно поднимается под драгом по dwell, БЕЗ активации** — `IWindow.BringToFront()` →
  `IWindowWorkerService.RaiseWithoutActivation()` (Win32: `SetWindowPos` + `SWP_NOACTIVATE`).
  **Почему не `Activate()`:** взятие фокуса заставляет ОС отобрать mouse capture (`WM_CAPTURECHANGED`), наш
  обработчик честно считает это потерей capture — и **отменяет драг**. Поднимаем только z-order; фокус даём
  ПОСЛЕ дропа, когда отменять уже нечего. На macOS это `orderFront:`, а НЕ `makeKeyAndOrderFront:`.
- **Ограничение:** поднимается окно, которое ВИДНО под курсором. Полностью скрытое за другим не поднять —
  наводиться не на что. Так же ведут себя и сами ОС (там роль «поднимателя» играет таскбар/Exposé).
- Внешние OLE-драги этим не задеты — там окно называет сама ОС.

- **Фаза 0 — общий `VisualRenderer` (переиспользуемый, НЕ частный под призрак).** Аналог UWP
  `RenderTargetBitmap` + `XamlReader`: `Render(IUIComponent visual | string aumlText, size?, scale)` →
  `RenderTargetImage`. Промоушен уже существующего offscreen-пути (`HeadlessWindowRenderer` /
  тестовый `OffscreenTestRenderer` + `AumlLoader.Load(text)` для текстового входа) в продакшн-сервис:
  measure+arrange корня → свой `RenderCache` + RenderTarget-презентер → record→process→draw→wait-idle →
  `RenderTargetImage` (его `ResolveTexture`). Проверить, показав результат обычным `Image`/`DrawImage`.
  *Это ФУНДАМЕНТ и для `VisualBrush`/`DrawingBrush` (TILE_BRUSH_PLAN), и для превью/миниатюр/дизайнера.*
  **Марки-тонкость — РЕШЕНО:** живой on-screen элемент печём через **отдельный `RenderCache` с нуля,
  живущий ПАРАЛЛЕЛЬНО** кэшу окна (у `VisualRenderer` свой кэш по определению). Словари юнитов — поля
  экземпляра кэша (`_recordedUnits`/`_orderByControl` и пр.), так что коллизии нет; уже-валидный компонент
  параллельный кэш просто читает, не пере-рендерит → общие марки не трогаются. AUML-текст / detached-визуал
  → свежее изолированное дерево, тем более чисто.
  **Lifetime — одноразовый:** `VisualRenderer` = `IDisposable`, `create → render → (readback) → dispose`;
  никакой реконсиляции/attachment-цикла (как `OffscreenTestRenderer`). Для призрака возвращаем CPU-bitmap и
  сразу освобождаем GPU-текстуру; для превью/брашей — живой `RenderTargetImage` (владеет потребитель).
- **Фаза 1 — окно-призрак.** `DragGhostWindow` (layered/transparent/topmost/click-through), пиксели —
  из readback слепка Фазы 0, следует за курсором. *Веха: картинка элемента летает над всем экраном.*
- **Фаза 2 — движок in-window (уровень 1).** `DragDropManager` + attached-свойства + порог смещения +
  `Mouse.Capture` + routed `DragEnter/Over/Leave/Drop` + доставка в `ICommand`; `DragData`-абстракция.
  Демо-таб: reorder внутри списка + перенос между двумя панелями, всё через VM-команды.
- **Фаза 3 — кросс-оконный, одна аппка (уровень 2).** Сессия app-global; hit-test по экранным
  координатам через все окна (сверху вниз по z-order); окно-призрак уже готов из Фазы 1. Демо: перенос
  между двумя окнами аппки.
- **Фаза 4 — Behaviors + кастомный вид.** `DragSourceBehavior`/`DropTargetBehavior`; `DragTemplate` как
  альтернатива слепку (когда нужен purpose-built вид призрака, а не копия). Триггер-подсветка
  `IsDragOver`.
- **Фаза 5 (СДЕЛАНО, Windows) — OLE drop-in.** WM_DROPFILES (`WS_EX_ACCEPTFILES`) убран, вместо него
  `RegisterDragDrop` + `IDropTarget` на HWND КАЖДОГО окна, один раз при создании и на всю жизнь окна.
  `IDataObject` читается СРАЗУ в `DataPackage` (CF_HDROP → `DataFormats.Files`, CF_UNICODETEXT/CF_TEXT →
  `DataFormats.Text`), дальше — тот же путь, что у in-app драга: `AllowDrop`-таргетинг, `IsDragOver`,
  каретка вставки, spring-load, автоскролл, `DropCommand`.
- **Фаза 6 (СДЕЛАНО, Windows) — OLE drag-out.** `DoDragDrop` на pump-потоке, наши данные → `IDataObject`
  со стандартными форматами. Опт-ин на источнике: `DragDrop.AllowExternalDrag` (+ одноимённое свойство у
  `DragSourceBehavior`).

### Как это устроено (Фазы 5-6)

- **Платформенный контракт (Core):** `INativeDragDrop` (регистрация drop-таргета на окно + `BeginDrag`)
  и `INativeDropSink` (что движок отдаёт наружу). Windows-реализация — `WindowsDragDrop` (OLE);
  для macOS сюда же ляжет `NSDraggingDestination`/`beginDraggingSessionWithItems`, для Linux — XDND
  и data-device Wayland. Публичный API движка при этом не меняется — ради этого и заведена
  абстракция payload'а.
- **Форматы платформенно-НЕЙТРАЛЬНЫ:** `DataFormats.Text` / `DataFormats.Files` (`string` / `string[]`).
  Вьюмодель пишет `data.Get(DataFormats.Files)` один раз — и это работает на любой платформе.
- **Потоки.** Колбэки drop-таргета приходят на оконный (pump) поток ВНУТРИ модального цикла чужого
  приложения. Ничего из дерева там не трогается: колбэк запоминает точку и ПОСТИТ работу в loop-поток,
  а ОС отвечает эффектом, посчитанным на прошлом move (кадр задержки). Блокирующий вызов из OLE-цикла в
  наш render-loop повесил бы оба приложения. Drag-out симметричен: `DoDragDrop` (модальный, блокирующий)
  запускается через приватное сообщение на скрытое окно — то есть на pump-потоке, а loop-поток продолжает
  рисовать; исход приходит колбэком.
- **Позиция курсора.** Во время чужого драга наше окно не получает mouse-move, поэтому платформенная
  точка публикуется в `MouseDevice.SetExternalPosition` — и вся машинерия drop'а (каретка, автоскролл,
  хит-тест) работает без единой правки.
- **Призрак.** На внешнем драге картинку несёт ОС (`IDragSourceHelper.InitializeFromBitmap`), а не наше
  layered-окно: тот же самый испечённый premultiplied-BGRA битмап (`DragGhostImage`), только отдан
  платформе. На macOS туда же ляжет `NSImage` — контракт один.
- **Живой CLR-payload переживает выход в ОС:** drag-out из нашего окна обратно в наше окно приходит
  нашим же `Win32DataObject`, и drop-таргет разворачивает исходный `IDataPackage` (никакой сериализации).
- **Эффект по умолчанию для чужого груза — Copy** (Ctrl=Copy, Shift=Move, Ctrl+Shift=Link), пересечённый с
  тем, что разрешил источник. Отвечать Move на чужой файл — значит велеть тому приложению удалить оригинал.
- **Требование STA:** OLE живёт только в single-threaded apartment, поэтому точке входа приложения нужен
  `[STAThread]` (как в WPF/WinForms/Avalonia). Если его нет — в лог уходит предупреждение, OS-мост выключается,
  а весь in-app drag-drop продолжает работать.
  *Что такое apartment (в двух словах, потому что термин мутный):* это не поток, а «комната» с правилом, кто
  вправе трогать живущие в ней COM-объекты. **STA** = ровно один поток; вызовы извне идут не напрямую, а через
  очередь сообщений скрытого окна этого потока — поэтому объекту не нужны локи, но поток ОБЯЗАН качать сообщения.
  Наш `IDropTarget` вызывает чужой процесс и привязан к окну, а окно потоко-аффинно — STA и есть механизм, который
  доставляет межпроцессный вызов на правильный поток. MTA-варианта drag-drop в Windows нет.
  *Практические следствия, на которые мы наступили:* без насоса сообщений `DoDragDrop` крутится вхолостую и не
  зовёт ни одного колбэка; блокировать OLE-колбэк нельзя (он держит всю квартиру — встанет и приложение-источник);
  а возврат нашего же драга на наше окно приходит БЕЗ прокси (та же квартира) — отсюда и живой CLR-payload.
  *Вариант «движок сам владеет STA-pump-потоком» рассмотрен и ОТКЛОНЁН* — AppKit требует главный поток процесса,
  так что это добавило бы платформенно-условный старт ради одной строки. Подробности и замеры — `TECH_DEBT.md`,
  раздел «Платформенный слой / потоки».

Уровни 1–2 (Фазы 0–4) — самодостаточная поставка, закрывает почти все нужды редактора. Уровень 3
(Фазы 5–6) — отдельная платформенная работа, публичный API не меняет (за то и абстракция данных).

### Что по уровню 3 ещё НЕ сделано

- **macOS / Linux** — реализованы только контракты (`INativeDragDrop`/`INativeDropSink`) и точка
  регистрации в оконном воркере; самих реализаций (AppKit `NSDraggingDestination` + `NSDraggingSession`,
  XDND/Wayland) нет. Без них на этих платформах работает всё, кроме обмена с чужими приложениями.
- **Mac-каталог курсоров** — см. «Открытые вопросы» ниже (для in-app фидбэка).
- **Форматы сверх Text/Files** (HTML, RTF, картинки, произвольные байты) — `IDataPackage` их несёт,
  но платформенный маппинг пока только для двух базовых.
- **Deferred rendering** (отдать формат лениво, в момент дропа) — сейчас payload рендерится по запросу
  из уже готовых значений; тяжёлые данные (сгенерированный файл на лету) потребуют своего хука.

---

## Defaults / из коробки (batteries-included)

Что работает «просто так», когда повесил `AllowDrag`/`AllowDrop` — без ручной возни.

### Дефолт (встроено) — сверено с кодом 2026-07-27
- ✅ **Коллекции (любой `ItemsControl`/`ListBox`/`TreeView`)** — reorder внутри + перенос между списками.
  Позиция приезжает в `DragDropEventArgs` двумя полями: `InsertIndex` (номер) и `InsertBefore` (ССЫЛКА на
  айтем, перед которым лечь). **Пользоваться надо `InsertBefore`** — индекс считается ДО удаления из
  источника и в reorder'е внутри одного списка успевает протухнуть.
- ✅ **Индикатор вставки** — `DropInsertionIndicator` (темизированный адорнер): каретка ПОПЕРЁК потока
  айтемов (учитывает `WrapPanel`/`StackPanel.Orientation`), в дереве — режим рамки для «в дети».
- ✅ **Автоскролл у краёв** — таймерный, с рампой по глубине захода в полосу; скорость настраивается
  attached-свойством `DragDrop.AutoScrollSpeed` на `ScrollViewer`.
- ✅ **Подсветка drop-таргета** — attached `IsDragOver` (используется в теме/триггерах).
- ✅ **Copy vs Move по модификатору** — Ctrl = Copy, иначе Move; курсор меняется живьём.
- ✅ **Esc = отмена** (class-handler на туннелирующем `PreviewKeyDown`), ✅ **порог старта** — берётся из
  настройки ОС (`PlatformSettings.DragThreshold`, на Windows `SM_CXDRAG`/`SM_CYDRAG`), сравнение по-осевое.
- ✅ **Призрак** — снимок элемента, с оффсетом +12px от курсора, в layered-окне с **попиксельной альфой**
  (`AC_SRC_ALPHA` + `ULW_ALPHA`): прозрачность настоящая, DWM уважает альфу битмапа. Равномерного
  приглушения всего призрака нет — `SourceConstantAlpha` = 255; если захочется «притушить» карточку целиком,
  это одно значение в `Win32DragGhost`.
- ✅ **Spring-loading** — `ISpringLoadable`; потребители `TabItem` (активация табы) и `TreeViewItem`
  (раскрытие узла). Задержка — **не наша константа**, а `PlatformSettings.HoverTime` (настройка пользователя:
  Win32 `SPI_GETMOUSEHOVERTIME`, обычно 400 мс; на macOS ляжет springing delay).
- ✅ **Подъём окна под драгом** — задержался над другим нашим окном → оно выходит вперёд, не забирая фокус
  (см. «Фаза 3, как она устроена» выше). Та же задержка, что у spring-load.
- ✅ **Мульти-выделение** — тащится всё выделение `ListBox`, на призраке бейдж-счётчик (реальный
  темизированный контрол, снятый в битмап). *Здесь, а не в «опт-ин»: работает по умолчанию.*
- ✅ **Кастомный вид призрака** — `DragTemplate` (attached + свойство у `DragSourceBehavior`).

### Что можно покрутить (и где — граница осмысленная, не случайная)

Числа не зашиты. Но они лежат в РАЗНЫХ местах, и разница содержательная: смешаешь — и читающий решит,
что значение пришло от системы, хотя его придумали мы.

- **`PlatformSettings`** — то, что настроил ПОЛЬЗОВАТЕЛЬ в ОС; мы только спрашиваем:
  `DragThreshold` (`SM_CXDRAG`/`SM_CYDRAG`, сравнение по-осевое), `HoverTime` (`SPI_GETMOUSEHOVERTIME` —
  задержка spring-load и подъёма окна), `DoubleClickTime`.
- **`DragDropOptions`** — НАШИ подкрутки, про которые ОС ничего не знает: `AutoScrollBand`,
  `AutoScrollSpeed`, `GhostCursorOffset`, плюс `SpringLoadDelayMs` / `WindowRaiseDelayMs` (отрицательное
  значение = «следовать настройке пользователя», это дефолт).
- **Attached-свойство** — там, где один элемент должен отличаться от остальных: `DragDrop.AutoScrollSpeed`
  на конкретном `ScrollViewer`. Ноль = «взять общий дефолт», причём читается ЖИВЬЁМ: метаданные
  attached-свойства строятся один раз при статической инициализации, поэтому вписать туда значение опции
  значило бы заморозить его на старте и молча игнорировать поздние изменения.
- **Остаётся константой** `AutoScrollTickMs = 16` — это интервал кадра, а не предпочтение; вынести его
  наружу означает только дать возможность сделать скролл рваным.

### Опт-ин / позже (НЕ дефолт)
- ❌ **Drag-handle (грип ≡)** — НЕ сделан. Старт драга нельзя ограничить отдельной «ручкой»; сейчас тащится
  любой элемент с `AllowDrag`.
- ❌ **Анимация раздвигания** — НЕ сделана (соседи не расступаются под точкой вставки).
- ✅ **Drag наружу, в другие приложения** — опт-ин `DragDrop.AllowExternalDrag`, Windows. Приём (drop-in)
  наоборот включён ВСЕГДА — окно принимает файлы/текст из коробки, ничего включать не нужно.

### Движковый готча
**Виртуализация.** Замысел был «считать по ДАННЫМ». **Фактически** `ComputeInsertion` сканирует
РЕАЛИЗОВАННЫЕ контейнеры (`ItemContainerGenerator.RealizedIndices`) — но результат отдаётся наружу как
`InsertBefore` (ссылка на айтем данных), поэтому на виртуализованном списке позиция не едет: реализованы
всегда те строки, что видны, а именно среди них и находится точка вставки. Держать в голове при правках:
опираться на `InsertBefore`, а не на числовой индекс.

## Открытые вопросы

- **Прозрачное окно-призрак, кросс-платформенно** — решено через `IDragGhost` над общим CPU-bitmap:
  Win = layered + `UpdateLayeredWindow` (несовместим со свопчейном именно он — потому без свопчейна),
  Mac = прозрачный `NSWindow` + `CALayer.contents`. Живой призрак (Win DComp / Mac CAMetalLayer) — отложено,
  т.к. развёл бы платформы. Фаза 1 подтвердит на практике.
- **DPI слепка — ЗАКРЫТО.** Печём в `RenderScale` окна-источника (`_ghostBakeScale`), пере-масштабируем
  СОБЫТИЙНО по `IDragGhost.DpiChanged` (хук `WM_DPICHANGED` через `WindowDpiWatcher`). Ключ, если полезешь
  туда снова: ре-скейл считается ОТНОСИТЕЛЬНО масштаба бейка, иначе на high-DPI старте призрак раздувается
  вдвое (bake × monitor).
- **Время жизни RT слепка — ЗАКРЫТО.** Битмап освобождается в `Reset()` (Drop/Cancel/потеря capture).
- **Порог старта — ЗАКРЫТО.** `PlatformSettings.DragThreshold` (Windows: `SM_CXDRAG`/`SM_CYDRAG`),
  сравнение по-осевое; используют `DragDrop`, `TabItem`, `ListBoxItem`.
- **Auto-scroll — ЗАКРЫТО** (таймерный, с рампой; скорость через `DragDrop.AutoScrollSpeed`).
- **Отмена — ЗАКРЫТО.** `Esc` (class-handler на `PreviewKeyDown`) + потеря capture (скриншотилка, Alt-Tab)
  → `CancelDrag`: без Drop, источнику приходит `DragCompleted` с `Effects=None`.
- ❗ **Touch/pen — ОТКРЫТО.** По-прежнему mouse-only; за будущей Pointer-абстракцией.
- **Mac-каталог стандартных курсоров — ЗАКРЫТО.** `Cursor` больше не носит нативный хэндл: он описывает
  `CursorType` (или файл), а платформа за `INativeCursors` резолвит и кэширует форму. `WindowsCursors` мапит
  на `IDC_*` (+ shipped `dragcopy.cur`, которого в Win32 нет), `MacOSCursors` — на `NSCursor` через
  `MacOSCursorType` (`operationNotAllowed` / `dragCopy` / `dragLink` там НАТИВНЫЕ; отсутствующие формы —
  Wait/Help/UpArrow/диагональные ресайзы — осознанно сведены к ближайшим, см. комментарий в файле).
  Регистрация — `Cursor.Platform = …` в ctor платформы, тем же приёмом, что `Clipboard.Current`.
- **Мульти-монитор / DPI-per-monitor — ЗАКРЫТО для призрака** (событийный ре-скейл при переходе на монитор
  с другим масштабом, проверено вживую в обе стороны) и для hit-test'а (экранные координаты физические,
  `PointToClient` делит на DPI-скейл окна).

---

## Что осталось (сводка по состоянию на 2026-07-27)

Всё ниже — НЕ сделано. Порядок — по моей оценке пользы, но приоритет за тобой.

1. **`DragDrop.DoDragDrop(...)` — старт драга из кода.** Сейчас драг начинается только жестом.
2. **macOS / Linux-реализации** `INativeDragDrop`, `IDragGhost` (контракты и точки регистрации готовы).
3. **Drag-handle** — старт драга только за «ручкой».
4. **Форматы сверх `Text`/`Files`** (HTML, RTF, картинки, произвольные байты) + **deferred rendering**
   (отдавать тяжёлый формат лениво, в момент дропа).
5. **Анимация раздвигания соседей** под точкой вставки.
6. **Touch/pen** — за Pointer-абстракцией.
7. **`DragCursor` в `DragDropEventArgs`** — ручной override курсора (`Handled` уже есть, пришёл вместе с
   routed-событиями).
8. **Tunnel-варианты routed-событий** (`PreviewDragEnter/Over/Leave/Drop`) — если понадобится veto с уровня
   родителя.

_(Закрытые пункты: «z-order при кросс-оконном драге» — окно спрашивается у ОС, плюс подъём окна под драгом
(см. «Фаза 3, как она устроена»); «routed-события» — см. раздел «Routed-события (контрол-уровень)».)_
