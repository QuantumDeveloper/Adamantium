# Анализ производительности и архитектуры: игра + ECS

_2026-07-17. Код под `C:\AdamantiumEngine\Adamantium\Adamantium`. Активная конфигурация Sandbox: сервисы `InputService`,
`TransformService` (update) + `RenderingService`/`ForwardRenderingProcessor` (render). Сцена = один импортируемый F-15._
_Дополняет [`PERF_ANALYSIS.md`](PERF_ANALYSIS.md) (2026-06-23, GPU-сериализация игры-в-панели): там — «почему 375→155»._
_Секции A–D — что резать в кадре; **E** — архитектура ECS (как Entity держит позицию и т.д.); **F** — архитектура
рендер-слоя. В конце — **словарь терминов** простым языком (все «умные» слова расшифрованы там)._

## TL;DR

- **Кадр:** игра целиком (Update + полный 3D-рендер + copy) идёт на КАЖДЫЙ presented-кадр без rate-limit (A) → самый
  крупный рычаг. ECS всё пересчитывает безусловно, без dirty-флагов (B). Доступ к компонентам аллоцирует и лочится
  поэлементно (C). Per-draw сабмишн избыточен: ребайнд шейдера/буферов на каждый меш (D).
- **Архитектура (E):** это **объектно-ориентированный сцен-граф**, а не data-oriented ECS. Две стратегические слабости —
  (1) **позиция сущности абсолютная в мире, иерархических трансформов нет** (родитель не двигает детей), (2) компоненты —
  heap-объекты в per-entity коллекции, а не плотные массивы. Сильные стороны — editor-first (bindable INPC-компоненты) и
  camera-relative точность в больших мирах. Обе слабости лечатся эволюционно.
- **Рендер-слой (F):** фундамент **современный и сильный** (shader objects, dynamic rendering, BDA, descriptor heap,
  Slang), но **слой над ним примитивен** — нет render graph, один forward-проход, CPU-driven подача по одному объекту с
  ре-байндами, одна очередь, один поток записи команд. «Не самый оптимальный» — верно; чинится наращиванием слоёв поверх
  правильного фундамента, а не переписыванием. Первый и самый ценный шаг — минимальный render graph.

Что **уже хорошо** (не трогать): кэш динамич. состояния пайплайна, персистентно-замапленные CB, frustum culling, lock-free
снапшот сервисов, шина shared-surface (≈0). Детали — в конце каждой секции.

---

## План работ по фазам (чеклист статуса)

Ссылки в скобках — на детальные находки по секциям выше.

### Phase 1 — CPU-провалы (безопасно, поведение-сохраняющее). ✅ COMMITTED c3da6b0 (+ render-thread always-on 6284b84)
- [x] `Entity.GetComponent/GetComponents` — убрать try/catch + `Console.WriteLine` с горячего пути (C2)
- [x] `EntityComponentCollection.Get/GetAll/Contains` — один лок через `ItemsSpan`; `GetAll` — одна аллокация (C1/C2)
- [x] `LightManager.Update` — dirty-гейт `_lightsDirty` вместо 3 LINQ-сканов/кадр (B2)
- [x] `ForwardRenderingProcessor` — `GetActive` вынесен в per-Draw, `Material` — из цикла рендереров (C4/C5)

### Phase 3 — иерархические трансформы. ✅ COMMITTED 5016e13, проверено (модель целая, куллинг ок, размер ок)
> **Корректировка:** оказалась НЕ ломающей. Импортёр [`EntityImportTemplate`] уже строит иерархию (`new Entity(parent…)`)
> и кладёт ЛОКАЛЬНЫЕ пер-нодовые трансформы (DAE-ноды) — сломано было только вычисление. Миграция импортёра не понадобилась.
- [x] `Transform.CalculateFinalTransform` — `world = local × parent`; camera-shift один раз в конце (E.1, E.6/1)
- [x] `TransformService` — берёт `AbsoluteWorld` родителя (top-down); `TransformMetaData.AbsoluteWorld` (camera-independent)
- [x] `OrientedBoundingBox.Transform(Matrix4x4F)` + `BoundingSphere.Transform(Matrix4x4F)` — новый примитив
- [x] `Box/SphereCollider.UpdateForCamera` — баунды по композированной мировой матрице (фикс «распадается при приближении»)
- [x] Чистка API: удалены каскадные сеттеры `SetPosition/SetRotation/SetPivot/SetPivotRotation/SetScalingRotation`,
  вызовы → свойства `.Position/.Rotation/…` (импортёрный `SetPosition` каскадил → модель уезжала вдвое дальше)

### Phase 3b — движки/ротаторы self-apply. ✅ СДЕЛАНО, собрано (0 ошибок), UNCOMMITTED. ⚠ нужен твой прогон инструментов в редакторе
- [x] `Move/Translate/TranslateRight|Up|Forward/TranslatePivot/Rotate/RotateRight|Up|Forward/RotatePivot(+R|U|F)/
  DivideScale/MultiplyScale/SetScaleFactor/SetBaseScale/Reset*` — убран `Traverse`, применяют к СВОЕМУ узлу (дети — через
  иерархию); удалены 13 осиротевших статических хелперов. Игровой рендер эти методы не использует → игру не задевает,
  но Move/Rotate-инструменты редактора надо прогнать вживую.

### Phase 2 — dirty-флаг + CPU-хвосты. ⏳ ЧАСТИЧНО (B1 COMMITTED c22556c; +~100 fps)
- [x] `Transform` — `IsWorldDirty` в сеттерах Position/Rotation/BaseScale/ScaleFactor/Pivot/PivotRotation (B1)
- [x] `TransformService` — пересчёт только при dirty / сдвиге камеры (мировая зависит от ПОЗИЦИИ камеры, не поворота →
  mouse-look бесплатен) / смене пивота; распространение dirty на детей при движении узла (B1)
- [x] Коллайдеры — O(n²)-цикл схлопнут в O(n), лишний `ClearData` убран (B3)
- [~] Развязка мировой от камеры (E.6/2) — полностью НЕ нужна: `AbsoluteWorld` уже camera-independent (для композиции
  детей), `WorldMatrixF` остаётся camera-relative; игровая камера в нуле → пересчёт по её позиции почти не триггерится
- [x] Кэш viewProj — используем готовый `Camera.ViewProjectionMatrix` (`World * ViewProjectionMatrix`) в `DrawEntity` (D4)
- [x] Мёртвый `FinalMatrices.Values.ToArray()` — унесён в комментарий: per-frame аллокация убрана, скелетный WIP сохранён (D5)
- [x] `Entity.TraverseInDepth` пул `Stack` + `TraverseByLayer` пул `Queue` (C3) — `[ThreadStatic]` **claim/release** (вложенный
  вызов находит пул пустым и берёт свежий) → реентрант-safe, без per-call аллокации. UNCOMMITTED.

### Phase 4 — рендер-слой (после ECS; фундамент НЕ трогать). ⏳ НЕ НАЧАТО
- [ ] Минимальный render graph — проходы + ресурсы + авто-барьеры (F.5/1)
- [ ] GPU-driven геометрия: батч по пайплайну + инстансинг + indirect; BDA-адресация → stateless draw (F.5/2)
- [ ] Многопоточная запись команд (secondary / неск. primary) (F.5/3)
- [ ] Async compute для пост/линий/частиц на отдельной очереди (F.5/4)
- [ ] Double-buffer командных буферов игры по frame-in-flight (F.5/5)
- [ ] Затем shadow map / depth pre-pass / пост-цепочка — уже поверх render graph (F.5/6)

### Задел на масштаб (доступ к компонентам, когда сущностей станет много). ⏳ НЕ НАЧАТО
- [ ] per-entity `Dictionary<Type,IComponent>` (O(1)) вместо линейного скана (E.2, E.6/4)
- [ ] SoA/архетипы для горячих компонентов (Transform, renderable); OOP-компоненты — для редких/editor (E.6/3)
- [ ] Модель отложенных структурных изменений вместо грубых локов (E.3, E.6/5)

### Побочный фикс — краш ресайза от render-thread-always-on. ✅ СДЕЛАНО, UNCOMMITTED. ⚠ нужен твой реальный drag-resize
Render-thread теперь всегда включён (6284b84) → при ресайзе AV `0xC0000005` в `vkQueueSubmit` на render-потоке: поток
рисовал/сабмитил против **устаревшего** свопчейна (loop-поток метил `IsRendererUpToDate=false`, а пересоздание
`ResizePresenter` стояло в `FrameEnded` — ПОСЛЕ отрисовки кадра). Фикс: (1) `IsRendererUpToDate` → `volatile` (кросс-поточная
видимость); (2) `ResizePresenter` перенесён в НАЧАЛО кадра (`WindowRenderService.BeginDraw`) — кадр всегда рисуется против
свежего свопчейна; `Presenter.Resize` ждёт device-idle и идёт на том же render-потоке, сериализовано с Submit/Present.
Пережил 120 программных `MoveWindow`-ресайзов, лог чист. `MoveWindow` не 100% повторяет тайминг ручного драга — проверить тебе.

---

## A. Архитектурный рычаг №1 — развязать частоту рендера игры от UI

**Цепочка кадра:** `EntityServiceManager.Draw` → `OnDrawStarted` → `GameApplication.ServiceManagerOnDrawStarted`
([GameApplication.cs:30](../Adamantium/Adamantium.Game/GameApplication.cs#L30)) → `GameService.RunGames` →
`Parallel.ForEach → Game.RunOnce` ([GameService.cs:61-76](../Adamantium/Adamantium.Game/GameService.cs#L61)). `RunOnce`
([Game.cs:255](../Adamantium/Adamantium.Game/Game.cs#L255)) делает **Update + полный Draw + FrameFinished без единой
проверки «менялось ли что-то»**.

**Почему `DesiredFPS` игры не работает:** цикл `Game.StartGameLoop` (читает `DesiredFPS`/`IsFixedTimeStep`,
[Game.cs:448-489](../Adamantium/Adamantium.Game/Game.cs#L448)) стартует только из `Run()`
([Game.cs:293](../Adamantium/Adamantium.Game/Game.cs#L293)). Хостовая игра создаётся через
`GameService.CreateGame → InitializeBeforeRun` ([Game.cs:185-188](../Adamantium/Adamantium.Game/Game.cs#L185)) и **`Run()`
не зовётся** → `gameLoopThread` не запускается, игру тикает только внешний `RunOnce`. Значит `DesiredFPS=60`
([Game.cs:66-67](../Adamantium/Adamantium.Game/Game.cs#L66)) сейчас **ни на что не влияет**.

**Фикс:** rate-gate в `RunGames`. Аккумулятор на `GameKey`; пропускать `item.Game.RunOnce(time)`, пока
`accum < item.Game.TimeStep` (`TimeStep = 1/DesiredFPS`, [Game.cs:170](../Adamantium/Adamantium.Game/Game.cs#L170)), иначе
`accum += FrameTime`. На пропущенном кадре — ничего: панель композитит последний shared-surface. Single-buffer безопасен —
backpressure-гард `if (_sharedSurface.ConsumeValue < _lastProduced) return;`
([RenderTargetGameOutput.cs:81](../Adamantium/Adamantium.Game.Core/RenderTargetGameOutput.cs#L81)) не даёт перезаписать
кадр во время сэмпла. Итог: UI ~375, игра ~60.

> Rate-gate поднимает СРЕДНИЙ FPS, но не удешевляет один игровой кадр. За стоимость кадра отвечают B/C/D — они и определяют
> достижимый потолок. A множит их ценность (B/C/D не исполняются на ~2/3 кадров), но одно другое не заменяет.

---

## B. ECS update — безусловный пересчёт (нет dirty-флагов)

**Корень:** ни у `Transform`, ни у `TransformMetaData`
([TransformMetaData.cs](../Adamantium/Adamantium.ECS/ComponentsBasics/TransformMetaData.cs)) нет dirty/version-флага,
поэтому всё ниже пересчитывается всегда.

1. **[КРУПНО] `TransformService` пересчитывает мировую матрицу КАЖДОЙ сущности каждый кадр × активные камеры.**
   [TransformService.cs:37-85](../Adamantium/Adamantium.Engine/EntityServices/TransformService.cs#L37) →
   `Transform.CalculateFinalTransform`
   ([Transform.cs:709-726](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L709)): полный
   `Matrix4x4F.Transformation(...)` + лишний widening-каст `(Matrix4x4)matrix`
   ([Transform.cs:721-722](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L721)) на каждый узел, даже если
   ничего не двигалось. **Фикс:** `bool IsDirty` на `Transform`, ставить в существующих `SetProperty`-сеттерах
   Position/Rotation/Scale/Pivot ([Transform.cs:75-133](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L75));
   т.к. `relativePosition` зависит от позиции камеры
   ([Transform.cs:712](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L712)), хранить в метадате позицию камеры
   и пересчитывать узел только если `IsDirty` ИЛИ камера сдвинулась. O(all) → O(moved). Заодно убрать double-каст.

2. **[ЛЁГКИЙ ВЫИГРЫШ] `LightManager.Update` пересобирает 3 списка LINQ'ом каждый кадр — хотя они ведутся инкрементально.**
   [LightManager.cs:93-103](../Adamantium/Adamantium.Engine/Managers/LightManager.cs#L93):
   `DirectionalLights/SpotLights/PointLights = _lights.Where(...).ToList()` ×3, а `AddLight` уже кладёт свет в нужный список
   ([LightManager.cs:160-168](../Adamantium/Adamantium.Engine/Managers/LightManager.cs#L160)). Зовётся из
   [TransformService.cs:47](../Adamantium/Adamantium.Engine/EntityServices/TransformService.cs#L47). **Фикс:** удалить тело
   `Update()` или гейтить `_lightsDirty`. Минус 3 скана + 3 аллокации/кадр.

3. **Коллайдеры: O(colliders²) + `ClearData` с реаллокацией на узел на камеру.**
   [TransformService.cs:70-77](../Adamantium/Adamantium.Engine/EntityServices/TransformService.cs#L70): вложенный i/j-цикл,
   `ClearData()` стирает весь per-camera словарь
   ([BoxCollider.cs:20-41](../Adamantium/Adamantium.ECS.Components/BoxCollider.cs#L20)), потом `UpdateForCamera` пере-добавляет
   только текущую камеру (при >1 камере переживает лишь последняя — латентный баг). **Фикс:** один цикл
   `foreach collider: collider.UpdateForCamera(camera)` (оно перезаписывает `ColliderData[camera]`, `ClearData` не нужен);
   гейтить dirty из B1.

4. **Камера: `Update` безусловно каждый тик** (view-матрица, оси, `UpdateFrustum`, viewProj).
   [InputService.cs:314](../Adamantium/Adamantium.Engine/EntityServices/InputService.cs#L314) →
   [Camera.cs:269-336](../Adamantium/Adamantium.ECS.Components/Camera.cs#L269), без проверки «камера двигалась?». **Фикс:**
   звать `Camera.Update`, только когда input реально менял Rotation/Position/Type этот кадр.

---

## C. Доступ к компонентам — аллокации + поэлементные локи (hot в update И рендере)

1. **`GetComponents<T>()` = `new List<T>()` + `.ToArray()` на каждый вызов на узел каждый кадр.**
   [EntityComponentCollection.cs:39-53](../Adamantium/Adamantium.ECS/EntityComponentCollection.cs#L39). Горячие вызовы:
   `TransformService.cs:60` (Collider), `ForwardRenderingProcessor.cs:89` (MeshRendererBase). **Фикс:** у сущностей обычно
   один Collider/MeshRenderer — неаллоцирующий `Get<T>()` или `GetAll<T>(List<T> reuse)`; для коллайдеров закэшировать ссылку.

2. **`Get<T>` = линейный скан с Monitor-локом НА КАЖДЫЙ элемент** (индексер `this[i]` перелочивает тот же `SyncRoot`).
   [EntityComponentCollection.cs:19-32](../Adamantium/Adamantium.ECS/EntityComponentCollection.cs#L19) +
   [AdamantiumCollection.cs:492-500](../Adamantium/Adamantium.Core/Collections/AdamantiumCollection.cs#L492). Плюс
   `Entity.GetComponent` обёрнут в try/catch + `Console.WriteLine`
   ([Entity.cs:241-252](../Adamantium/Adamantium.ECS/Entity.cs#L241)). **Фикс:** взять лок один раз и идти по сырому
   backing-массиву (или вести `Dictionary<Type,IComponent>` → O(1)); убрать try/catch+Console.

3. **`GetEnumerator` боксит struct-энумератор + лочится; `TraverseInDepth` аллоцирует `Stack` на вызов.**
   [AdamantiumCollection.cs:92-98](../Adamantium/Adamantium.Core/Collections/AdamantiumCollection.cs#L92) (боксинг),
   [Entity.cs:315-330](../Adamantium/Adamantium.ECS/Entity.cs#L315) (`new Stack<Entity>()`). **Фикс:** возвращать struct-энумератор,
   обходить `Dependencies` по индексу, переиспользовать пулled `Stack` (обход однопоточный).

4. **`GetComponent<Material>()` внутри цикла рендереров** — материал per-entity, а лукап per-renderer.
   [ForwardRenderingProcessor.cs:101](../Adamantium/Adamantium.Engine/EntityServices/ForwardRenderingProcessor.cs#L101). **Фикс:** вынести из цикла.

5. **`CameraManager.GetActive(Window)` (lock + dict) на каждый узел** в обходе рисования.
   [ForwardRenderingProcessor.cs:50](../Adamantium/Adamantium.Engine/EntityServices/ForwardRenderingProcessor.cs#L50), хотя
   камера уже добыта в [RenderingService.cs:157](../Adamantium/Adamantium.Engine/EntityServices/RenderingService.cs#L157). **Фикс:** читать из поля процессора.

---

## D. Per-draw GPU-сабмишн — избыточное состояние на каждый меш

1. **[КРУПНО] Шейдер ребайндится КАЖДЫЙ draw, даже когда pass не менялся.** `EffectPass.ApplyHeap`
   ([EffectPass.cs:190-198](../Adamantium/Adamantium.Graphics/Effects/EffectPass.cs#L190)) — `foreach stage → BindShader`
   (+ `BindShader(GeometryBit, null)`) = **3 `vkCmdBindShadersEXT` на каждый `Apply`**, а `Apply` — раз на меш. Все меши
   F-15 идут через ОДИН pass → все ребайнды кроме первого избыточны. **Фикс:** `_lastAppliedPass` в `GraphicsDevice`,
   пропускать блок BindShader, если `CurrentEffectPass == this` уже выставлен на этом cmd-буфере (инвалидировать в
   `BeginDraw` рядом с `_stateInitialized=false`, [GraphicsDevice.cs:912](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L912)).

2. **VB и IB биндятся ДВАЖДЫ на draw.** `MeshRendererBase.Draw`
   ([MeshRendererBase.cs:68](../Adamantium/Adamantium.ECS.Components/MeshRendererBase.cs#L68),
   [:76](../Adamantium/Adamantium.ECS.Components/MeshRendererBase.cs#L76)) биндит VB/IB, затем `DrawIndexed` биндит снова
   ([GraphicsDevice.cs:1401-1403](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L1401)). **Фикс:** убрать один из двух.

3. **`vkGetBufferDeviceAddress` на каждый CB на каждый draw.**
   [EffectPass.cs:173](../Adamantium/Adamantium.Graphics/Effects/EffectPass.cs#L173) → `Buffer.GetDeviceAddress`
   ([Buffer.cs:390-395](../Adamantium/Adamantium.Graphics/Buffer.cs#L390)) зовёт драйвер каждый раз, хотя адрес страницы
   пула стабилен. **Фикс:** кэшировать device-address на `Buffer`/странице пула.

4. **WVP = 2 матричных умножения на меш; `View*Proj` не кэшируется.**
   [ForwardRenderingProcessor.cs:102](../Adamantium/Adamantium.Engine/EntityServices/ForwardRenderingProcessor.cs#L102).
   **Фикс:** `viewProj = View*Proj` раз на кадр, в цикле `World * viewProj`.

5. **`GraphicsDevice.Submit` аллоцирует 5×`List` + 3×`ToArray` + `SubmitInfo` на сабмит** (×~2 сервиса × ~155/с).
   [GraphicsDevice.cs:1115-1169](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L1115). **Фикс:** переиспользуемые
   поля-массивы (как `commandBuffersArray`, [GraphicsDevice.cs:95-97](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L95)).

6. **Transition-барьеры аллоцируют `List`/`ToArray`/`new[]` на кадр** ([GraphicsDevice.cs:629](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L629),
   [:666](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L666), [:728](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L728)).
   Мелко. (`BufferBarrier` `new[]{barrier}` [:1471](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L1471) — это
   compute-путь UI-обводки, НЕ игровой hot-path.)

7. **Нет сортировки/батчинга/инстансинга** — наивная per-object подача. `ClearColor` зря выставляется в per-mesh цикле
   ([ForwardRenderingProcessor.cs:134](../Adamantium/Adamantium.Engine/EntityServices/ForwardRenderingProcessor.cs#L134)).

**Уже эффективно (не гоняться):** кэш динамич. состояния `SetDrawingState`
([GraphicsDevice.cs:1302-1377](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L1302), re-emit только при изменении);
персистентно-замапленные CB ([Buffer.cs:133-140](../Adamantium/Adamantium.Graphics/Buffer.cs#L133), аплоад = один
`MemoryCopy`); **frustum culling ЕСТЬ**
([ForwardRenderingProcessor.cs:59-63](../Adamantium/Adamantium.Engine/EntityServices/ForwardRenderingProcessor.cs#L59); оговорка:
узел БЕЗ Collider не рисуется вообще, [:77](../Adamantium/Adamantium.Engine/EntityServices/ForwardRenderingProcessor.cs#L77) —
возможный источник «пропавших» мешей); Deferred/ForwardPlus — мёртвые заглушки, активен только `ForwardRenderingProcessor`
([AdamantiumGame.cs:37](../Adamantium/Adamantium.Game.Sandbox/AdamantiumGame.cs#L37)); lock-free снапшот сервисов
([EntityServiceManager.cs:146-168](../Adamantium/Adamantium.ECS/EntityServiceManager.cs#L146)); шина shared-surface ≈0.

---

## E. Архитектура ECS — сильные/слабые стороны и сравнение с лучшими решениями

### E.0 Что это на самом деле

Adamantium «ECS» — это **объектно-ориентированный сцен-граф**, а не data-oriented ECS. `Entity` — объект
(`PropertyChangedBase`) с `Transform` + per-entity коллекцией компонентов-**объектов** (`EntityComponentCollection`,
[Entity.cs:40-43](../Adamantium/Adamantium.ECS/Entity.cs#L40)). «Системы» = сервисы (`EntityService`/`EntityProcessor`),
обходящие дерево сущностей. **Нет архетипов, нет SoA, нет плотных массивов компонентов, нет кэшированных запросов.** Ближе
всего к Unity legacy GameObject/Component (+ Transform), но даже более OOP: компоненты — наблюдаемые INPC-объекты в
залоченной коллекции.

**Таксономия для сравнения:**
- **Data-oriented archetype ECS** — Unity DOTS/Entities, Bevy, flecs: компоненты в плотных SoA-массивах по архетипам;
  системы — запросы по сигнатуре; линейный cache-friendly обход; структурное изменение перемещает сущность между архетипами.
- **Sparse-set ECS** — EnTT, flecs: компоненты в sparse-set (dense + sparse index), O(1) add/remove/get, быстрый обход
  одного типа.
- **Сцен-граф трансформов** — Unity `Transform`, Godot `Node3D`, Unreal `USceneComponent`: **локальный** трансформ +
  указатель на родителя, `world = parent.world * local`, top-down propagate с dirty.

### E.1 Как Entity держит позицию — ГЛАВНАЯ слабость

**Факт (подтверждён в коде):**
- `Transform.Position` — **абсолютная позиция В МИРЕ**
  ([Transform.cs:99-103](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L99)). Локального пространства и
  parent-relative координат нет.
- Мировая матрица строится **только из собственных** Position/Rotation/Scale узла:
  `relativePosition = Position - camera.Owner.Transform.Position`
  ([Transform.cs:712](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L712)) →
  `Matrix4x4F.Transformation(...)` ([Transform.cs:716](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L716)).
  **Матрица родителя НЕ умножается.**
- «Иерархия» (`Entity.Owner`/`Dependencies`, `TraverseInDepth`) — дерево **владения/обхода**, не трансформа. При обходе
  всем узлам передаётся один общий pivot корня (`generalCenter = root.GetLocalCenter()`,
  [TransformService.cs:57-58](../Adamantium/Adamantium.Engine/EntityServices/TransformService.cs#L57)), но каждый узел
  использует свою абсолютную `Position`.

**Следствия (слабости):**
- **Перемещение/поворот/масштаб родителя НЕ двигает детей** — иерархических трансформов нет. Модель = плоский мешок
  world-позиционированных суб-мешей; нельзя подвинуть всю модель за корень, строить сочленённые иерархии через
  Entity-дерево (скелетная анимация идёт отдельно через `AnimationController.FinalMatrices`, а не через трансформ-дерево),
  инстанцировать префаб и переставить его, двигая корень.
- **Нет локального пространства** → редактирование в родительских координатах невозможно; `Position` ребёнка бессмысленна
  без знания, что она мировая.
- **Мировая матрица кэшируется ПО КАМЕРЕ** (`Dictionary<CameraBase, TransformMetaData>`,
  [Transform.cs:28](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L28),
  [43-54](../Adamantium/Adamantium.ECS/ComponentsBasics/Transform.cs#L43)) и пересчитывается каждый кадр. Это связывает
  трансформ с камерой и множит работу на число камер; мировой трансформ должен быть camera-independent.
- Нет dirty-распространения → полный пересчёт каждый кадр (см. B1).

**Сильная сторона подхода:** camera-relative рендеринг **решает точность float в больших мирах** (координаты у камеры около
нуля — техника из космо/планетарных рендеров). Но реализована на **неверном слое** — запечена в world-матрицу и по камере,
а должна быть view-time смещением (вычитать позицию камеры на этапе WVP).

### E.2 Хранение и доступ к компонентам

Компоненты — heap-объекты в per-entity `EntityComponentCollection` (`: TrackingCollection : AdamantiumCollection`). Плотных
массивов по типам нет; `GetComponent<T>` = линейный скан с локом на элемент (детали в C2). Против лучших: EnTT/flecs/DOTS
держат компоненты одного типа плотно → обход одного типа в разы быстрее и без GC, `get` = O(1). Здесь — обход дерева +
фильтр `GetComponent`, без кэшированных запросов.

### E.3 Системы / итерация / изменения

«Системы» = сервисы с Update/Draw, обходят дерево (`TraverseInDepth`) и фильтруют `GetComponent`. Нет системы запросов по
сигнатуре компонентов, нет авто-расписания зависимостей систем, нет параллелизма по системам. Потокобезопасность — грубые
локи (`SyncRoot` на коллекцию, лок на элемент); нет модели **отложенных структурных изменений** (command buffer на барьере
кадра), как в DOTS/flecs. `ServiceManager` при этом уже хорош — lock-free снапшот **сервисов** (не путать с обходом сущностей).

### E.4 Сильные стороны (не потерять при эволюции)

- Простая, понятная OOP-модель; легко навесить поведение и расширять в рантайме (любой компонент).
- Компоненты — first-class наблюдаемые (INPC) объекты → **прямой data-binding редактора** к свойствам компонентов (сильно
  для designer/editor-first движка; у чистого DOTS этого из коробки нет).
- Camera-relative точность в больших мирах.
- Уже сделанный lock-free снапшот сервисов.

### E.5 Слабые стороны (свод)

- **Не data-oriented** → cache-unfriendly, GC, локи; плохо масштабируется на 10k–100k сущностей.
- **Нет архетипов/запросов** → обход всего дерева + линейный фильтр.
- **Нет иерархических трансформов** (главное) + нет локального пространства.
- **Нет dirty-флагов** → полный пересчёт каждый кадр.
- **Per-camera хранение мировой матрицы** связывает трансформ с камерой.
- **INPC на горячих данных** (`Transform`) — оверхед для того, что должно быть plain fields в bulk-апдейте.

### E.6 Путь эволюции (прагматично, без переписывания с нуля)

1. **Иерархические трансформы:** хранить ЛОКАЛЬНЫЕ pos/rot/scale, добавить `World = parent.World * local`, top-down
   propagate в `TransformService` с dirty-флагом. Camera-relative оставить как view-time смещение (вычитать позицию камеры
   на этапе WVP, не в world-матрице). Закрывает главный пробел корректности и попутно даёт dirty (B1).
2. **Развязать world от камеры:** одна camera-independent мировая матрица на сущность; camera-relative — на этапе вида.
   Убирает `Dictionary<Camera,…>` и ×камеры.
3. **Для масштаба:** горячие компоненты (`Transform`, renderable) → к SoA/плотным массивам + типизированные запросы
   (archetype или sparse-set), оставив OOP-компоненты для editor-facing/редких. Гибрид как Unity (GameObject + DOTS) или flecs.
4. **`GetComponent<T>` с локом-на-элемент → per-entity `Dictionary<Type,IComponent>`** (O(1)) или настоящий компонент-стор.
5. **Модель отложенных структурных изменений** вместо грубых локов.
6. **Сохранить сильное:** INPC/bindable компоненты для редактора и camera-relative точность — просто перенести на
   правильный слой.

### Вердикт

Зрелый OOP сцен-граф уровня «Unity classic», отлично подходящий для **editor-first** движка с data-binding, но с двумя
стратегическими пробелами: (1) **нет иерархических трансформов** (позиция мировая, родитель не двигает детей) и (2) **не
data-oriented хранение**, ограничивающее масштаб. Оба лечатся эволюционно, не ломая сильные стороны — начинать логично с (1),
т.к. он даёт и корректность иерархии, и dirty-флаги «в подарок».

---

## F. Рендер-слой — архитектура (сильные/слабые стороны, сравнение)

### F.0 Что под капотом

Фундамент — **современный Vulkan**, и это сильно:
- **Shader objects** (`vkCmdBindShadersEXT`, [EffectPass.cs:190-198](../Adamantium/Adamantium.Graphics/Effects/EffectPass.cs#L190))
  вместо монолитных PSO — нет комбинаторного взрыва пайплайнов.
- **Dynamic rendering** (переходы + begin rendering в BeginDraw,
  [GraphicsDevice.cs:629](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L629)/[:666](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L666)/[:728](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L728))
  — нет объектов `VkRenderPass`/`Framebuffer`.
- **Descriptor heap + BDA** (buffer device address) — «bindless»-фундамент: ресурсы адресуются, а не биндятся по слотам.
- **Slang** как компилятор шейдеров.

Примитивы выбраны правильно — это опережает многие самодельные движки (см. правило «только современный Vulkan»).

### F.1 Как устроен кадр рендера

`RenderingService` ([RenderingService.cs](../Adamantium/Adamantium.Engine/EntityServices/RenderingService.cs)) —
**фиксированный конвейер**: `BeginDraw` (ставит RT/depth/MSAA/blend, стартует ОДИН dynamic-rendering проход,
[RenderingService.cs:105-141](../Adamantium/Adamantium.Engine/EntityServices/RenderingService.cs#L105)) → `Draw` →
`DrawProcessors` (один `ForwardRenderingProcessor`) → `EndDraw` (`Window.CopyOutput`,
[RenderingProcessor.cs:89-92](../Adamantium/Adamantium.Engine/EntityServices/RenderingProcessor.cs#L89)) → `Submit` →
`Present` (Mailbox) → `FrameEnded`. Командный буфер — **только Primary**
([GraphicsDevice.cs:549](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L549)), **пересобирается с нуля каждый кадр**.
Одна графическая очередь; `computeQueue` объявлена ([GraphicsDevice.cs:32](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L32)),
но не используется.

### F.2 Слабые стороны (архитектура, не микро-перф)

1. **Нет render graph (frame graph).** Барьеры/переходы ресурсов расставлены ВРУЧНУЮ в BeginDraw
   ([629](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L629)/[666](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L666)/[728](../Adamantium/Adamantium.Graphics/GraphicsDevice.cs#L728)),
   нет авто-планирования проходов, барьеров и переиспользования памяти RT. Добавить тени/пост/depth-prepass чисто некуда:
   каждый новый проход = ручная возня с барьерами и RT. **Главный архитектурный пробел.**
2. **Только один проход — forward.** Нет shadow map, depth pre-pass, пост-обработки (bloom/SSAO/tonemap);
   Deferred/ForwardPlus — мёртвые заглушки. Освещение базовое, в одном проходе.
3. **CPU-driven, подача по одному объекту.** Нет инстансинга, indirect draw, GPU-culling, батчинга по пайплайну/материалу.
   N мешей = N draw'ов с полной установкой состояния. Современные рендеры — GPU-driven (indirect + GPU-culling + meshlets).
4. **Per-draw ре-байнды состояния** (секция D): шейдер/буферы биндятся на каждый меш, `GetBufferDeviceAddress` дёргается
   на каждый CB — хотя descriptor heap + BDA как раз позволяют сделать draw'ы почти stateless. **Фундамент bindless есть,
   но луп его не использует.**
5. **Один поток записи команд, одна очередь, серийно.** Primary-only, нет secondary/нескольких primary для многопоточной
   записи; нет async compute (линии/пост могли бы идти на compute-очереди параллельно).
6. **Нет перекрытия кадров для игрового рендера.** Игра лок-степлена к UI (секция A); нет двойной буферизации командных
   буферов по frame-in-flight на стороне игры, чтобы перекрыть CPU-запись с GPU-исполнением.
7. **`CopyOutput` (resolve→shared) каждый кадр** — нужен для панели, но это ещё один проход по той же очереди (замерено ≈0,
   не рычаг, но часть серийной цепочки).

### F.3 Сильные стороны (не потерять)

- Современные примитивы (shader objects, dynamic rendering, BDA, descriptor heap, Slang) — редкость у самодельных движков.
- Кэш динамического состояния `SetDrawingState` — уже режет лишние ре-эмиты.
- Персистентно-замапленные constant-buffer'ы — аплоад без map/unmap.
- Frustum culling на CPU есть.
- Shared-surface интеграция игра→UI-панель — чистая (zero-copy import, ≈0 sync).

### F.4 Сравнение с лучшими решениями

| Возможность | Лучшие (UE5 / Frostbite / Bevy-render / Granite) | Adamantium |
|---|---|---|
| Организация проходов | render graph (авто-барьеры, aliasing, cull) | ручные барьеры, один проход |
| Подача геометрии | GPU-driven: indirect + GPU-culling + инстансинг | CPU, по одному объекту |
| Материалы/ресурсы | bindless-массивы, stateless draw | descriptor heap ЕСТЬ, но per-draw bind |
| Запись команд | многопоточная (secondary / неск. primary) | один Primary, один поток |
| Очереди | graphics + async compute + transfer | одна graphics |
| Проходы качества | shadows, depth-prepass, пост, TAA | только forward |

### F.5 Путь эволюции (по слоям, фундамент НЕ трогать)

Примитивы правильные — расти слоями поверх:
1. **Минимальный render graph** — проходы + ресурсы + авто-барьеры. Заменяет ручные переходы в BeginDraw и открывает
   тени/пост/depth-prepass без ручной возни. **Самый ценный первый шаг рендер-слоя.**
2. **GPU-driven геометрия** — батч по пайплайну, инстансинг, indirect draw; через BDA per-object данные адресуются, а не
   биндятся → draw'ы почти stateless (закрывает D1–D4 архитектурно, а не заплатками).
3. **Многопоточная запись команд** (после render graph) — secondary / несколько primary.
4. **Async compute** для пост/линий/частиц на отдельной очереди.
5. **Double-buffer командных буферов игры по frame-in-flight** — перекрыть CPU-запись с GPU-исполнением.
6. Затем shadow map, depth pre-pass, пост-цепочка — уже тривиально поверх render graph.

### Вердикт рендера

Фундамент современный и сильный (правильные Vulkan-примитивы), но **слой над ним примитивен**: нет render graph, один
forward-проход, CPU-driven подача с ре-байндами, одна очередь, один поток записи. «Не самый оптимальный» — верно; но чинится
**наращиванием слоёв поверх уже правильного фундамента**, а не переписыванием. Первый и самый ценный шаг — минимальный
render graph.

---

## Порядок внедрения (по выгоде)

1. **[архитектурно, крупнейшее] A — rate-gate `Game.RunOnce`** по `DesiredFPS`: игра 155→~60, UI к ~375.
2. **[корректность+CPU] E.6/1 + B1 — иерархические трансформы с dirty-флагом** (world = parent*local, camera-relative в view):
   чинит «родитель не двигает детей» и убирает доминирующий O(all-entities×cameras) пересчёт разом. **B2** — удалить тело
   `LightManager.Update`.
3. **[доступ к компонентам] C1–C3** (неаллоцирующий `Get<T>`, одинарный лок, убрать try/catch; **C4/C5** вынести Material/GetActive).
4. **[per-draw] D1** guard ребайнда pass + **D2** двойной bind VB/IB + **D3** кэш device-address + **D4** кэш viewProj.
5. **[GC] D5/D6** аллокации Submit/барьеров, `Stack` в `TraverseInDepth`, мёртвый `FinalMatrices.Values.ToArray()`
   ([ForwardRenderingProcessor.cs:85](../Adamantium/Adamantium.Engine/EntityServices/ForwardRenderingProcessor.cs#L85)).

_Замеренные числа (375/155/3.3 мс) — из `PERF_ANALYSIS.md`; перед оптимизацией по ним стоит перемерить на текущем коде
(render-thread теперь всегда включён)._

---

## Словарь терминов (простым языком)

**ECS / сущности-компоненты-системы.** Сущность (Entity) — «объект в сцене» (модель, свет, камера). Компонент — кусок
данных на нём (позиция, меш, материал). Система — код, который каждый кадр проходит по сущностям и что-то делает
(двигает, рисует).

**OOP сцен-граф vs data-oriented ECS.** Два способа хранить это.
- *OOP сцен-граф* (у нас): каждая сущность — обычный объект в памяти, компоненты — тоже объекты, лежащие «кучей» у неё
  внутри. Просто и гибко, но объекты разбросаны по памяти → процессору дорого их перебирать, и сборщик мусора нагружен.
- *Data-oriented ECS* (Unity DOTS, Bevy, EnTT, flecs): компоненты ОДНОГО типа лежат подряд в одном большом массиве.
  Перебор тысяч сущностей идёт по сплошной памяти — в разы быстрее, без мусора. Сложнее в устройстве.

**Архетип (archetype).** Группа сущностей с ОДИНАКОВЫМ набором компонентов; их данные хранятся вместе плотными массивами.
Основа быстрых ECS. У нас архетипов нет.

**SoA (structure of arrays) / «плотные массивы».** «Массив структур» → «структура массивов»: вместо списка объектов
{поз, скорость} держат отдельно массив всех позиций и массив всех скоростей. Ложится в кэш процессора → быстрый перебор.

**Sparse-set.** Способ хранить компоненты так, чтобы добавить/убрать/найти был мгновенным (O(1)) и перебор быстрым. Основа
EnTT/flecs.

**dirty-флаг («грязный» флаг).** Пометка «это изменилось, пересчитать». Без него всё считается каждый кадр заново, даже если
ничего не двигалось. С ним пересчитывается только изменённое.

**INPC (INotifyPropertyChanged).** Механизм «объект сообщает об изменении своих свойств» — чтобы UI-редактор мог
подписаться и обновиться. Удобно для редактора, но это лишний вес на «горячих» данных вроде позиции.

**Локальное vs мировое пространство.** *Мировое* — координаты относительно всей сцены. *Локальное* — относительно родителя.
В нормальной иерархии ребёнок хранит локальные координаты, а мир = «мир родителя × локальное». У нас позиция сразу мировая,
локального нет → родитель не двигает детей.

**Иерархический трансформ.** Правило «мир ребёнка = мир родителя × его собственный трансформ», раздаваемое сверху вниз по
дереву. Даёт «подвинул корень — уехала вся модель». У нас этого нет.

**camera-relative (относительно камеры).** Приём: считать координаты относительно камеры, чтобы числа были маленькими около
нуля — иначе в больших мирах у float не хватает точности и всё «дрожит». Приём хороший, но у нас зашит не на том слое.

**WVP (World-View-Projection).** Три матрицы, которыми вершина переводится из своего пространства на экран: World (в мир),
View (глазами камеры), Projection (перспектива). Их перемножение — на каждый меш.

**Command buffer (командный буфер).** Список команд для видеокарты («забиндь то, нарисуй это»), который CPU записывает, а
GPU потом исполняет. У нас он один (Primary) и переписывается заново каждый кадр.

**Descriptor / descriptor heap / bindless.** *Дескриптор* — «указатель» на ресурс (текстуру, буфер) для шейдера.
Классически их биндят по слотам перед каждым draw. *Bindless / descriptor heap* — все ресурсы лежат в одной большой
«куче», шейдер берёт нужный по индексу/адресу, биндить перед каждым draw не надо. Фундамент у нас есть, но луп всё равно
биндит по-старому.

**BDA (buffer device address).** Возможность обращаться к буферу по прямому адресу в памяти GPU (как указатель), а не через
слот-биндинг. Позволяет делать «stateless» draw'ы. Есть в фундаменте.

**PSO (pipeline state object) vs shader objects.** *PSO* — заранее «запечённый» объект «шейдеры + всё состояние»; на каждую
комбинацию нужен свой → их плодятся тысячи. *Shader objects* (EXT_shader_object) — биндишь шейдеры и состояние по
отдельности, гибко, без взрыва комбинаций. У нас — современный вариант (shader objects).

**Dynamic rendering.** Современный Vulkan без старых объектов `RenderPass`/`Framebuffer` — проход начинается/кончается
командой. У нас так и есть.

**Slang.** Язык/компилятор шейдеров (современная замена HLSL/DXC). Основной компилятор шейдеров в движке.

**Render graph (frame graph).** «Граф проходов рендера»: описываешь проходы (тень, основной, пост) и их ресурсы, а система
САМА расставляет барьеры, переиспользует память и выкидывает лишнее. Стандарт в UE5/Frostbite. У нас нет — барьеры руками.

**Барьер (barrier) / переход (transition).** Команда синхронизации: «дождись, пока картинку допишут, прежде чем читать»
и «переведи текстуру в режим для чтения/записи». В render graph расставляются автоматически; у нас — вручную.

**Инстансинг / indirect draw / GPU-driven.** Способы рисовать много объектов дёшево: *инстансинг* — один вызов рисует N
копий; *indirect draw* — параметры draw'ов лежат в буфере, GPU сам их читает; *GPU-driven* — видеокарта сама решает, что
рисовать (culling на GPU). У нас — CPU по одному объекту.

**Frustum culling.** Отсечение того, что не попадает в поле зрения камеры, чтобы не рисовать зря. У нас на CPU есть.

**MSAA / resolve.** Сглаживание краёв многократной выборкой; *resolve* — свести многосэмпловую картинку в обычную.
Присутствует.

**Async compute / очередь (queue).** У видеокарты несколько «очередей команд»; *async compute* — гонять вычисления
(пост-эффекты, частицы) на отдельной очереди ПАРАЛЛЕЛЬНО основному рисованию. У нас всё в одной графической очереди.

**frame-in-flight / двойная буферизация.** Пока GPU рисует кадр N, CPU уже готовит кадр N+1 — для этого держат по 2–3
комплекта буферов. Для игрового рендера у нас этого перекрытия нет.

**Deferred / Forward / Forward+.** Стратегии освещения. *Forward* (у нас) — считаем свет сразу при рисовании объекта,
просто, но дорого при многих источниках. *Deferred/Forward+* — сложнее, но тянут сотни источников. У нас Deferred/Forward+
— мёртвые заглушки.
