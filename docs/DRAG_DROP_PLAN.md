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
  `Win32NativeWindowWrapper.CreateWindowExW` принимает ex-стиль. На обычных окнах уже стоит
  `Acceptfiles` (частичный WM_DROPFILES — заменим полноценным OLE на уровне 3).

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

- **`DragDrop`** (статический фасад): attached-свойства (`AllowDrag`, `DragData`, `AllowDrop`,
  `DropCommand`, `DragOverCommand`, `DragTemplate`, `AllowExternalDrag`, read-only `IsDragOver`), метод
  старта `DragDrop.DoDragDrop(source, data, effects)` для ручного/кодового старта, routed-события
  (`DragEnter/DragOver/DragLeave/Drop`, `GiveFeedback`).
- **`DragDropManager`** (app-global): держит активную сессию (payload, источник, эффект, окно-призрак);
  на каждый move — hit-test по экранным координатам через все окна аппки, ведёт DragEnter/Over/Leave,
  на up — Drop.
- **`DragData` / `DataPackage`** : `IDataPackage`: `Get<T>()`, `Set(object)`, `Contains(format)`,
  `GetFormats()`. In-app обёртка над CLR-объектом; OLE-обёртка над `IDataObject`.
- **`IDragGhost`** (платформенный контракт): `Show(bitmap, screenPos)` / `Move(screenPos)` / `Hide()` над
  ОДНИМ CPU RGBA-bitmap. Win32 = layered-окно + `UpdateLayeredWindow`; macOS = прозрачный `NSWindow` +
  `CALayer.contents`. Bake + readback (элемент → RT → bitmap) — общий, платформенно-нейтральный.
- **`DragSourceBehavior` / `DropTargetBehavior`** : Behaviors-обёртки над тем же ядром.
- **`DragEventArgs`** : `Data`, `Source`, `GetPosition(relativeTo)`, `Effects` (Copy/Move/Link/None) —
  таргет ставит его в `DragOver`, `DragCursor` (settable, перебивает дефолт-мапу курсора), `Handled`.

---

## Фазы

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
- **Фаза 5 (later, платформенный слой) — OLE drop-in.** Заменить WM_DROPFILES на `RegisterDragDrop` +
  `IDropTarget` на HWND каждого окна; читать `IDataObject` (CF_HDROP=файлы, CF_UNICODETEXT=текст) →
  бридж в `DragData`. Всегда включён. Windows первым, за платформенной абстракцией.
- **Фаза 6 (later, платформенный слой) — OLE drag-out.** `DoDragDrop` (блокирующий OLE-цикл), наши
  данные → `IDataObject` со стандартными форматами. Опт-ин на источнике (`DragDrop.AllowExternalDrag`).

Уровни 1–2 (Фазы 0–4) — самодостаточная поставка, закрывает почти все нужды редактора. Уровень 3
(Фазы 5–6) — отдельная платформенная работа, публичный API не меняет (за то и абстракция данных).

---

## Defaults / из коробки (batteries-included)

Что работает «просто так», когда повесил `AllowDrag`/`AllowDrop` — без ручной возни.

### Дефолт (встроено)
- **Коллекции (любой `ItemsControl`/`ListBox`)** — reorder внутри + перенос между списками (по индексу
  вставки). Флагманский сценарий.
- **Индикатор вставки (drop-line / зазор)** — линия/промежуток, показывающий КУДА ляжет айтем. Не опция —
  половина ощущения «хорошего DnD».
- **Автоскролл у краёв** — при удержании drag у края скроллируемой области (список / любой `ScrollViewer`)
  скролл с рампой скорости (ближе к краю — быстрее).
- **Подсветка drop-таргета** — через read-only `IsDragOver` (триггер-стейт; тема даёт рамку/заливку на
  элементе под курсором).
- **Copy vs Move по модификатору** — Ctrl зажат = Copy (где таргет разрешает), иначе Move → меняет
  `Effects` → сразу курсор.
- **Esc = отмена**, порог старта (клик ≠ drag).
- **Призрак** — снимок элемента с небольшим оффсетом от курсора + полупрозрачность.
- **Spring-loading (общий dwell-механизм)** — навёл-подержал во время drag → контейнер активируется/
  раскрывается. Встроенные потребители: `TabItem` (авто-активация табы) и `TreeView` (авто-раскрытие узла);
  экспандеры/аккордеоны через тот же `ISpringLoadable`.

### Опт-ин / позже (НЕ дефолт)
- **Drag-handle (грип ≡)** — ограничить старт перетаскивания отдельной «ручкой» (когда айтем содержит
  интерактив). Опт-ин.
- **Мульти-выделение** — тащить всё выделение разом, на призраке бейдж-счётчик. Для Selector-коллекций,
  фазой позже.
- **Анимация раздвигания** — соседи плавно расступаются под точкой вставки (Figma/iOS). Вяжется с
  layout-анимацией — позже.
- **OLE drag-наружу** — уровень 3, опт-ин на источнике (`AllowExternalDrag`).

### Движковый готча (заложить с Фазы 2)
**Виртуализация** (`TreeView`/`ItemsControl` виртуализованы): индекс вставки и автоскролл считать **по
ДАННЫМ** (модели коллекции), а НЕ по реализованным контейнерам — иначе на длинном виртуализованном списке
вставка/скролл поедут.

## Открытые вопросы

- **Прозрачное окно-призрак, кросс-платформенно** — решено через `IDragGhost` над общим CPU-bitmap:
  Win = layered + `UpdateLayeredWindow` (несовместим со свопчейном именно он — потому без свопчейна),
  Mac = прозрачный `NSWindow` + `CALayer.contents`. Живой призрак (Win DComp / Mac CAMetalLayer) — отложено,
  т.к. развёл бы платформы. Фаза 1 подтвердит на практике.
- **DPI слепка** — печь в device-масштабе элемента, пере-печь при смене DPI (зеркалит text-RT).
- **Время жизни RT слепка** — RT живёт на время сессии, освобождается на Drop/Cancel.
- **Порог старта** — `PlatformSettings` минимальная дистанция drag (как у Thumb), чтобы клик ≠ drag.
- **Auto-scroll** — при удержании drag у края скроллируемого таргета (nice-to-have, Фаза 4+).
- **Отмена** — `Esc` во время drag → Cancel (вернуть без Drop), снять capture, закрыть призрак.
- **Touch/pen** — сейчас mouse-only; за будущей Pointer-абстракцией (см. input-pointer-multitouch).
- **Mac-каталог стандартных курсоров** — `Cursors` сейчас хардкодит Win32 `LoadCursor` (`No`/`SizeAll`/…),
  то есть drag-курсоры пока Windows-only. Для фидбэка на Mac добавить маппинги через `MacOSCursorType`
  (`NSCursor.operationNotAllowed` / `dragCopy` / `dragLink`). Небольшая работа, нужна для кросс-платформенного
  курсор-фидбэка.
- **Мульти-монитор / DPI-per-monitor** — призрак и hit-test по экрану должны работать в физических
  координатах с учётом попадания курсора на монитор с другим масштабом.
