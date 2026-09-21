# План: развязка рендеринга в отдельный поток (композиторная модель)

## Контекст (зачем)

Сейчас весь кадр UI-приложения идёт **последовательно на одном потоке** `applicationLoopThread`
(`Adamantium.UI/UIApplication.cs:447`): `DrainPending`(ввод) → `AnimationManager.Tick` → layout-pass
(`ServiceManager.Update`) → build render-cache → GPU `Submit` → `Present`, всё сериализовано
`EntityServiceManager.syncObject`. Значит **любая тяжёлая операция layout** (структурная перестройка
сцены на 60k, ресайз в 4K, смена темы) блокирует весь кадр — приложение фризит, FPS проваливается.
В однопотоке этого не победить.

**Цель:** приложение всегда отзывчиво и держит стабильный FPS (60+, **без кэпа** — максимум, потом при
желании залочим) даже при мега-тяжёлых операциях layout. Достигается **композиторной моделью**
(как WPF MilCore / Chrome compositor / Android RenderThread / Flutter raster): рендер-поток владеет GPU
и презентует **независимо** от update-потока — пока update молотит тяжёлый layout, рендер продолжает
презентовать последний готовый кадр (и сам применяет дешёвые трансформы — скролл/анимацию), поэтому
фриза нет.

**Требования:**
1. Рендер-поток без кэпа — максимум FPS.
2. Неблокирующая композиция — рендер работает, пока update занят тяжёлым layout.
3. Анти-рывок — update **не убегает вперёд** рендера; часы анимации привязаны к презентации, чтобы после
   тяжёлого кадра анимация не «телепортировалась».
4. Композиторный скролл/анимация — дешёвые трансформы применяются на рендер-потоке для плавности даже
   при зависшем layout.

## Ключевая проблема (крус)

Retained-рендер сейчас на каждом кадре **ре-читает ЖИВЫЕ компоненты**: `RenderCache.World()`
(`RenderCache.cs:354`) собирает трансформ из живых `LocalTransform`/`VisualParent`; обход читает
`World(unit.Component)` (`:648`) и `unit.Update` (`:673`) перезаписывает `RenderData.TransformMatrix`;
`TransformTable.SetMatrix` читает живой `World(node)` (`:402`); scissor/clip читают живые
`RenderSize`/`ClipToBounds`. При этом `DrawCommand.RenderData` **УЖЕ снимает снапшот** World/Opacity/Clip
при записи (`DrawingContext.cs:143-151`, поле `RenderData.TransformMatrix` — `RenderData.cs:18`) — но
рендер-обход его игнорирует.

Пока рендер дёргает живые компоненты, он не может работать параллельно с update (который эти же
компоненты мутирует). **Сделать рендер-сторону самодостаточным снапшотом — фундамент всего плана.**

## Целевая архитектура

- **Update-поток** (пишет дерево): `DrainPending`(ввод), `AnimationManager` (структурные/геометрические
  изменения), layout-pass (measure/arrange → `Bounds`/`RenderSize`/`LocalTransform`), `RenderDirty`
  mark+clear, запись draw-команд (`component.Render` — только CPU), placement попапов (`LayoutPopups`).
  Формирует **иммутабельный `RenderPacket`** — **дельту, не всю сцену** (O(dirty), чтобы не потерять
  выигрыш retained-кэша: Clean реплеит ops, Partial ре-рендерит только грязное).
- **Render-поток** (владеет GPU): приём пакета → `BeginDraw` (fence/acquire) → applier
  (создание/тесселляция юнитов, bake, upload батчей, `TransformTable`, record/replay ops) → `Render` →
  `Submit` → `Present` → `FrameEnded` → сигнал update. **Один рендер-поток на все окна** (сериализует
  acquire/submit/present).
- **`RenderPacket`:** `BuildKind` (Clean/Partial/Full); для Partial — грязные компоненты + их draw-команды
  (каждая уже с `RenderData`-снапшотом); для Full — paint-order последовательность; на юнит **запечены**
  World/clip/scissor/cull/opacity/visibility/motion-slot (из существующих чистых CPU-хелперов
  `World`/`CumulativeClip`/`ResolveScissor`/`NodeOf`); для motion-нодов — матрицы (из
  `RenderDirty.MovedNodes`); плюс per-frame флаги (AnalyticAA, Background, projection, RenderScale).
  Транспорт — расширенный `RenderData`.
- **Двойная буферизация ПАКЕТА** (не retained-кэша — он GPU-резидентный и инкрементальный, остаётся
  единственным экземпляром на рендер-потоке; копировать его было бы разорительно). Два пакета, публикация
  через `Interlocked.Exchange`, swap на границе кадра рендера.
- **Backpressure (анти-рывок):** update **не перезаписывает** непотреблённый пакет — ждёт свободный слот
  (максимум 1 кадр вперёд). Часы анимации привязаны к **презентованным** кадрам (сейчас
  `AnimationManager.Tick` идёт по настенному `frameTime.FrameTime` — `UIApplication.cs:599` — это меняем
  на presentation-paced), чтобы после тяжёлого кадра анимация продолжалась плавно, а не скакала.
  Т.к. рендер uncapped и дешёвый (реплеит пакет), он почти всегда опережает update и не теряет пакеты —
  кэп рендера НЕ нужен для анти-рывка.
- **Композиторный скролл/анимация (payoff цели):** motion-ноды (`IsRenderMotionNode`/`TransformTable`)
  уже изолируют дешёвые трансформы (скролл = одна матрица, флип = одна матрица). Рендер-поток владеет
  `TransformTable` и продвигает эти трансформы **сам между пакетами** (presentation-paced) → скролл/анимация
  плавные даже при зависшем layout. Это и есть суть композитора.

## Синхронизация

- Present + Acquire на графической очереди НЕ под `_submissionSync` — один рендер-поток делает их
  последовательно. `_submissionSync` (процессный reentrant mutex, `GraphicsDevice.cs:120-121,402-410`)
  **ОСТАЁТСЯ** — защищает `Submit` от resource-loader device (async-аплоады) и от hosted-game.
- Resize: WM_SIZE → update-поток (маршалится через `Dispatcher.Post`→`DrainPending`) → **записывает
  намерение** в пакет/контрол-канал; рендер-поток делает `DeviceWaitIdle`+`RecreateSwapchain` на своём
  `FrameEnded` (там же, где сейчас — `SwapChainGraphicsPresenter.cs:334-341`).
- Пересоздание устройства (`RecreateDevicesAndServices`, `UIApplication.cs:254`) — **stop-the-world**
  барьер: рендер-поток паркуется в безопасной точке (после `Present`, до `BeginDraw`), join, recreate,
  release.
- Font/TextLayout/Geometry.Mesh — **заморозить в пакете** (иммутабельные снапшоты геометрии/текста): их
  измеряет update, а тесселлирует/растеризует render. `FontRenderer` per-device безопасен только пока
  один рендер-поток (рендер остаётся последовательным).

## Фазы (каждая отдельно тестируема, по возрастанию риска)

**Билд/запуск/проверка для каждой:** `dotnet build -p:Platform=x64`, запуск `artifacts/bin/net10.0`,
вкладка Layout (60k стресс — `LayoutViewModel`), FPS через `CalculateFps` (`UIApplication.cs:544`) +
тайминги `RuntimeStats`; проверять непрерывный скролл, drag вкладок, смену темы (стрелки Up/Down), resize.

- **Фаза 0 — Инструментация / baseline** (без изменения поведения). Пер-поточные счётчики + гейдж
  «latency пакета». Замер baseline FPS + разбивка фаз на 60k и на скролле — понять, что доминирует
  (render-side Full-walk vs CPU-пол). Файлы: `RuntimeStats.cs`, `DiagnosticsOverlayBehavior.cs`.
  **Риск: нет.**
- **Фаза 1 — Рендер-обход читает снапшот `RenderData`, а не живые компоненты (БЕЗ потоков).** Файлы:
  `RenderCache.cs` (World/ResolveScissor/ResolveBake/walk :339,:648,:673,:1183-1228), `RenderData.cs`,
  `DrawingContext.cs:143-151`. Пишем в `RenderData` resolved scissor/cull/motion-slot; обход берёт
  запечённые значения вместо живого `World()`. Пиксельно идентичный вывод; FPS ≥ (убирает O(depth) живой
  re-read — может даже вырасти). **Это де-рискует весь крус ДО появления потоков.**
  **Риск: НИЗКИЙ-СРЕДНИЙ.**
- **Фаза 2 — Выделить `RenderPacket`; разбить `RenderCache` на recorder(update)/applier(render); ВСЁ ЕЩЁ
  однопоточно.** Файлы: новый `RenderPacket`; `RenderCache.cs` (расщепить чистый CPU World/CumulativeClip/
  paint-order в recorder; оставить BuildUnitsFor/батчи/TransformTable/ExecuteOps в applier);
  `ForwardWindowRenderer.cs`, `WindowRenderService.cs`, `AdornerRenderProcessor.cs`,
  `PopupRenderProcessor.cs`; **заморозить payloads**. Loop-поток: layout → recorder → публикует пакет →
  (пока inline) applier. Нет второго потока ⇒ нет гонок, баги детерминированы. Замерить стоимость сборки
  пакета (постоянный налог). **Риск: СРЕДНИЙ.**
- **Фаза 3 — Applier на выделенный рендер-поток; двойная буферизация пакета; неблокирующая презентация +
  backpressure.** Файлы: `UIApplication.cs` (рендер-цикл :447-513, семафоры, барьер пересоздания :254),
  `EntityServiceManager.cs` (снять Draw/Present/OnFrameEnded с syncObject :142-185), `WindowRenderService.cs`
  (resize render-side :159-173). За флагом `SingleThreadedRender` для A/B (Фаза 2 = fallback). ≥60 FPS +
  **не фризит** на 60k; latency ≤ 1 кадр; нет tearing/UAF. **Риск: ВЫСОКИЙ.**
- **Фаза 4 — Композиторный скролл/анимация: рендер-поток продвигает motion-ноды/анимацию сам между
  пакетами (presentation-paced).** Payoff цели: скролл/анимация плавные при зависшем layout.
  **Риск: ВЫСОКИЙ (но на готовом фундаменте).**
- **Фаза 5 (опц.) — Пер-оконные рендер-потоки / мульти-очередь.** Только если замеры оправдают (требует
  пер-поточной эксклюзивности очереди или расширения `_submissionSync`). **Риск: ВЫСОКИЙ.**

## Риски (честно)

- **Фаза 3 — самая рискованная** (реальная конкуренция): *tearing* (митигируется иммутабельным
  опубликованным пакетом); *use-after-free на resize/пересоздании* (update демотируется до «запись
  намерения», render владеет `DeviceWaitIdle`+recreate + stop-the-world барьер); *шаринг
  FontRenderer/TextLayout/Geometry.Mesh* — самое тонкое (заморозка payloads = под-шаг Фазы 2).
- **Hosted 3D game** сейчас пампится INLINE в UI Draw (`GameApplication.cs:21` → `GameService.RunGames`)
  на своём device/queue с timeline-sync → ложится на UI рендер-поток «бесплатно» (аргумент ЗА split).
- Постоянный налог копии пакета (Фаза 2) должен быть меньше выигрыша overlap'а (Фаза 3), иначе split
  net-negative **для throughput**. НО для цели «анти-фриз» split оправдан независимо от throughput —
  рендер держит кадр во время тяжёлого layout, чего никакой O(delta) не даёт.

## Критичные файлы

- `Adamantium.UI/Rendering/RenderCache.cs` — recorder/applier split, живой→снапшот (:339, :532-856, :1183-1228).
- `Adamantium.UI/UIApplication.cs` — цикл, рендер-поток, handoff, барьер пересоздания (:447-513, :254).
- `Adamantium.UI/EntityServices/WindowRenderService.cs` — BeginDraw/Draw/EndDraw/Submit/Present/FrameEnded + resize (:103-173).
- `Adamantium.ECS/EntityServiceManager.cs` — syncObject фазы (:142-185).
- `Adamantium.Graphics/GraphicsDevice.cs` + `Adamantium.Graphics.Core/SwapChainGraphicsPresenter.cs` —
  `_submissionSync`, Acquire/Submit/Present/FrameEnded, swapchain recreate.
- Вторичные: `RenderData.cs`, `DrawingContext.cs:143-151`, `RenderDirty.cs`, `AdornerRenderProcessor.cs`,
  `PopupRenderProcessor.cs`.

## Проверка (end-to-end)

- Каждая фаза: билд x64 → запуск `artifacts/bin/net10.0` → вкладка Layout 60k (FPS/тайминги в оверлее),
  непрерывный скролл, drag вкладок, смена темы, resize/maximize; сравнить с baseline (Фаза 0).
- Фаза 1: designer read-back (`SaveFrameRaw`, `WindowRenderService.cs:228`) на пиксельную идентичность.
- Фаза 3+: искусственно тяжёлый layout-кадр (форс Full-walk на 60k) → проверить, что FPS рендера не
  проваливается и скролл/анимация плавные во время него; профилировать latency ввода (≤ 1 кадр); стресс
  resize/maximize/theme-swap на отсутствие UAF/tearing.

## Решения (согласовано 2026-07-12)

1. **Первый заход = Фаза 0 + Фаза 1** (baseline + рендер читает снапшот `RenderData` вместо живых
   компонентов, без потоков). Чистый выигрыш + фундамент круса до всякой конкуренции.
2. **Флаг `SingleThreadedRender`** — не самоцель; оставить лишь как удобство для дебага (простой fallback),
   не вкладываться в его вечную поддержку.
3. **Фаза 4 (композиторный скролл/анимация) — сразу за Фазой 3** — именно там реальный выигрыш
   отзывчивости.
