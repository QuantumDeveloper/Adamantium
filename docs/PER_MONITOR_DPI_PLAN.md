# Per-Monitor DPI-aware (PMv2) — план

_Живой документ. Дизайн согласован, реализация по фазам. Ссылки на код — `путь:строка`._

---

## 0. Проблема

Приложение **не DPI-aware** вообще (в windowing/render ни одного `SetProcessDpi*`, `WM_DPICHANGED`, ни scale-фактора окна; `dpi` в коде — только метаданные картинок). Следствия:

- На мониторе с масштабом ≠ 100% ОС растягивает битмап кадра → **мыло** (если процесс unaware), либо всё **мелкое** (если объявить aware, но не масштабировать).
- При переносе окна между мониторами с разным DPI — **ничего не происходит**, картинка не подстраивается.

Цель — **настоящий Per-Monitor-V2**: чётко на любом мониторе, живой отклик на `WM_DPICHANGED`, каждое окно знает свой DPI.

---

## 1. Модель координат: логические пиксели (как в WPF)

**Решение: DIP-модель.** Layout и проекция — в **логических DIP** (базис 96 DPI); физические client-пиксели = `DIP × DpiScale` покомпонентно.

- `DpiScale` — per-**window** **`Vector2` (X/Y, не скаляр!)**, источник `GetDpiForMonitor` → `dpiX/dpiY / 96` (не `GetDpiForWindow` — тот отдаёт один uniform-номер). Анизотропный скейл на десктопе редок, но реален (растянутые режимы, физически неквадратные панели, телефон/планшет), и в движке он **уже есть** через `RenderTransform` `ScaleX≠ScaleY` (схлопывается в `sqrt(det)` в `ComputeFringeWidth`) → `Vector2` честнее и почти бесплатен на уровне данных.
- `Window.ClientWidth/ClientHeight` становятся **логическими** (сейчас = физическим из `GetClientRect`). Это семантический сдвиг → аккуратно протянуть по `PointToClient`/вводу/`GetProjectionMatrix`.
- Проекция (`Window.GetProjectionMatrix`) остаётся в логическом `ClientSize`.
- Swapchain/viewport/RT = **физический** размер = `ClientSize(лог.) × DpiScale`.

Это ложится на **существующий механизм `RenderScale`** (`WindowRendererBase.cs:60`): проекция логическая, а presenter/viewport уже сайзятся `ClientSize × RenderScale` (`ForwardWindowRenderer.cs:36`, `WindowRendererBase.cs:110`) — причём **по каждой оси отдельно** (`ClientWidth×ScaleX`, `ClientHeight×ScaleY`), так что `RenderScale` естественно становится `Vector2 = DpiScale` (сейчас скаляр — расширить). Для on-screen DPI это и есть `RenderScale = DpiScale`.

> `RenderScale` сейчас **designer-only** (headless-zoom, `WindowRenderService.RenderHeadlessFrame` → `:186`); on-screen путь держит `1.0` и ресайзит presenter по сырому `ClientWidth` (`WindowRenderService.cs:166`). Нужно завести scale в on-screen путь и примирить этот ресайз на `× scale`.

DPI и designer-zoom **перемножаются** (`deviceScale = worldScale × RenderScale`), поэтому одна ось масштаба покрывает оба.

---

## 2. Что масштабируется само, а что надо пересоздавать

Ключевое различие: `RenderScale` — это **растровый** масштаб (та же геометрия в больший таргет = SSAA). Он даёт правильный масштаб и AA, но **не** плотность геометрии.

| Компонент | Статус при смене DPI |
|---|---|
| **Обводки** (`GpuStrokeRenderComponent`) | ✅ целиком на GPU (compute-expander). Нужно только **прокинуть scale** — `deviceScale = worldScale × RenderScale` уже есть (`:272-274`). |
| **AA-фриндж заливок** (`GpuFillRenderComponent.ComputeFringeWidth` `:175`) | ✅ адаптивен: `1px / deviceScale`. Подхватится через `RenderScale`. |
| **SDF rect / rounded-rect** (`RectBatch`) | ✅ аналитические, разрешение-независимые. Ребилд не нужен. Шаблон SDF-семейства. |
| **Эллипс / круг** (solid, unrotated) | ✅ **можно в SDF-батч** (сиблинг `RectBatch`; `EllipsePayload` уже есть). Настоящий эллипс rx≠ry: нулевая изолиния `length(local/half) − 1` = **точная форма**, AA через `fwidth` (тот же трюк, что `SdRoundBox`). Резолюшн-независимо, self-AA, из тесселяции **убирается**. Fallback: сектор/частичная дуга, равномерная обводка (нужна истинная дистанция → `sdEllipse`/градиент-нормировка), поворот (вбить угол в инстанс), gradient/image fill. |
| **Тело тесселированных кривых** (произвольные path/безье, частичные дуги, `CombinedGeometry`) | ❌ плотность зашита при флаттеринге и **scale-unaware**: дуги `∝ локальный размер`, потолок 200 (`ArcSegment.cs:7,160`); безье — **фикс. 20 сэмплов** (`PolyQuadraticBezierSegment.cs:60`, `CubicBezierSegment.cs:43`), коряво даже на 1×. Скейл сетки множит и ошибку хорды → фасетки. Нужна **device-scale-aware плотность** (Ф3). |
| **Атлас глифов** (`FontRenderer.RenderScale` `UIRenderComponent.cs:343`) | ❌/⚠️ раст под конкретную плотность; на смене DPI — **ре-раст / бакет по device-размеру**, иначе мыло. |
| **Кастомный non-client** (`Win32WindowWorker.HandleNcCalcSize` `:244`) | ❌ хардкод 7px/+1px в физических пикселях → **× DpiScale**. |

**Два класса кривой геометрии (архитектурная развилка):**
- **SDF-семейство** — аналитическое, резолюшн-независимое, батчем, self-AA, **ребилд при DPI не нужен**: rounded-rect ✓ + эллипс/круг (см. ниже), потенциально capsule/ring/regular-n-gon/line.
- **Произвольная геометрия** — без дешёвого closed-form SDF (path/безье/частичные дуги/`CombinedGeometry`) → тесселяция с **device-scale-aware плотностью** (Ф3).

Итог: `RenderScale` — необходимая половина (масштаб растра + обводки + фриндж). Достаточность = **вынести примитивы в SDF-семейство** (снимает их с тесселяции) + **device-scale-aware тесселяция остатка** (Ф3) + ре-раст атласа глифов.

---

## 3. Фазы

**Порядок согласован:** сначала фундамент+масштаб (Ф0-2, окно реагирует и масштабируется — глифы/фриндж/обводки уже ок), потом адаптивная тесселяция (Ф3, убрать фасетки/мыло). Ф3 — не опциональна: это планка качества, «просто доскейлить» сетку её не даёт.

### Ф0 — фундамент
- Объявить процесс **Per-Monitor-V2** через `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` при старте (без манифеста; P/Invoke в `Win32Interop`, гейт под Windows).
- Ввести `DpiScale` (`Vector2`, X/Y) как per-window свойство (+ событие смены). Источник — `GetDpiForMonitor` (`dpiX/dpiY`).
- Зафиксировать DIP-семантику `ClientWidth/Height`.

### Ф1 — Win32-интеграция
- Начальный DPI при создании окна → `DpiScale`, стартовый размер.
- **`WM_DPICHANGED` (0x02E0)**: взять предложенный OS-rect (`lParam`), `SetWindowPos` **синхронно** на OS-потоке; managed-часть (обновить `DpiScale`, инвалидация layout/geometry) — **замаршалить на loop-поток через `DispatchInput`** (в стиле уже сделанной thread-sync работы: `Win32WindowWorker` resize/activate/capture/raw-input).
- Ввод: физ. координаты → DIP (`÷ DpiScale`) в `PointToClient`/`ScreenToClient` (`:459/:530`).
- NC-бордеры (`HandleNcCalcSize`) × `DpiScale`.
- (Опц.) `WM_GETDPISCALEDSIZE` для плавного ресайза на границе мониторов.

### Ф2 — рендер-пайплайн
- Завести `RenderScale = DpiScale` в **on-screen** путь; примирить `WindowRenderService.cs:166` (сырой `ClientWidth`) на `× RenderScale` как в базе.
- Проекция = DIP; swapchain/viewport = физ. Проверить, что presenter пересоздаётся под физ. размер и не ловит OOM (суб-аллокатор уже смягчает).

### Ф3 — адаптивная тесселяция под device-плотность

**Цель: адаптировать ВСЮ тесселяцию под DPI-скейл так, чтобы недостатки — плотность, кривизна, фасетки, мыло — были максимально незаметны на любом масштабе.** «Просто доскейлить» сетку не годится: скейл матрицы (или любой доп. скейл — это одно и то же для растеризатора) множит и ошибку хорды; плотность зашита при флаттеринге и сейчас **scale-unaware**. Единственный способ добрать чёткость — **ре-флаттерить от вектора с плотностью от device-scale**.

- **Сначала вынести SDF-семейство.** Эллипс/круг → SDF-батч (сиблинг `RectBatch`, `EllipsePayload` уже есть; rounded-rect уже там). Резолюшн-независимо, self-AA, **убирает примитивы из тесселяции вообще** → сокращает объём Ф3. Тесселяция ниже — только для **произвольной** геометрии. (Не строго DPI: помогает и на 1× — крупные примитивы перестают фасетить.)
- **Единая метрика — сагитта в device-px** (напр. ≤ 0.25 device-px): плотность считать от `deviceScale = worldScale × RenderScale`, а не от логического размера. При анизотропном DPI брать `max(deviceScaleX, deviceScaleY)` — сагитта суб-пиксельна по **обеим** осям. Один критерий на все примитивы. (SDF-эллипс анизотропию держит сам — это просто эллипс.)
- **Дуги** (`ArcSegment.cs:160`): формулу сегментов `× deviceScale`; фикс-потолок 200 (`:7`) поднять/сделать динамическим от device-размера.
- **Безье** (`PolyQuadraticBezierSegment.cs:60`, `CubicBezierSegment.cs:43`): фикс. 20/`rate` → **адаптивный счётчик** от сагитты в device-px. Побочно чинит и коробку 1× (крупные безье корявы независимо от DPI).
- **Остальные пути тесселяции тел** (`RenderUnit.cs:413`, `UIRenderComponent`/`GeometryRenderComponent`, ellipse/rounded/CombinedGeometry) — протянуть `deviceScale` **единообразно во все** пути флаттеринга, чтобы не осталось scale-unaware углов.
- **Триггер (выбор внутри Ф3):**
  - **Ре-флаттер на смену `deviceScale`** — точная плотность, но rebuild меша (врезка в инвалидацию рендер-кэша). Суб-аллокатор (`DeviceMemoryAllocator`) уже гасит стоимость пересоздания буферов.
  - **Тесселяция под текущий максимум device-scale**, дальше только скейл вниз — без rebuild на каждый шаг, ценой лишних треугольников.
  - **Бакетирование `deviceScale`** (ре-флаттер только при переходе порога) — компромисс, чтобы не дёргать на микро-изменениях зума/переносе.
- **Глифы:** раст под device-плотность (ре-раст / size-бакет атласа); MSDF держит до ~2× сам, дальше — новый бакет.
- **Обводки/фриндж:** уже device-aware (`GpuStroke`/`ComputeFringeWidth`), только довести `RenderScale`.

**Критерий готовности Ф3:** на 100/125/150/175/200% и при плавном переносе между мониторами кривые и текст — **без видимых фасеток и без мыла**, размеры корректны, без заметных «скачков» плотности при пересечении порогов бакетов.

### Ф4 — полировка (отдельно)
- Мульти-монитор: каждое окно свой `DpiScale`, корректный перенос.
- Designer: `DpiScale × zoom` (обе оси в один `RenderScale`).
- macOS backing-scale (свой механизм, отдельный заход).

---

## 4. Точки врезки

- `Adamantium.UI/Platforms/Windows/Win32WindowWorker.cs` — `messageTable`/`CustomWndProc` (добавить `WM_DPICHANGED`), `HandleResize` (уже маршалится), `HandleNcCalcSize` (×DpiScale), `PointToClient`/`ScreenToClient`.
- `Adamantium.UI.Core` `Window`/`IWindow` — `ClientWidth/Height` (→ DIP), новый `DpiScale` + событие, `GetProjectionMatrix`.
- `Adamantium.UI/Rendering/WindowRendererBase.cs:60` + `EntityServices/WindowRenderService.cs:166,184` — `RenderScale` в on-screen путь.
- `Adamantium.UI/Rendering/RenderUnits/RenderUnit.cs:413`, `UIRenderComponent.cs`, `GpuFillRenderComponent.cs` — device-px tolerance тел + ре-тесселяция на смене scale.
- `Adamantium.UI/Rendering/RenderUnits/GpuStrokeRenderComponent.cs:272` — только довести `RenderScale`.
- `Adamantium.UI/Rendering/RectBatchCollector.cs` + `RectItem.cs` + `Effects/BatchEffect.fx` (пасс `RectBatch`) — **шаблон** для `EllipseBatchCollector`/`EllipseItem`/пасса `EllipseBatch`; `Rendering/Payloads/EllipsePayload.cs` — источник.
- `Adamantium.Win32`/`Win32Interop` — `SetProcessDpiAwarenessContext`, `GetDpiForMonitor` (dpiX/dpiY → `Vector2`), `WM_DPICHANGED` (P/Invoke в выделенном interop-классе).

---

## 5. Нюансы

- **`WM_DPICHANGED` двухсторонний:** OS даёт и новый DPI, и рекомендованный rect — применять **оба** (иначе окно «прыгает»). Win32-вызовы (`SetWindowPos`) — синхронно на OS-потоке; managed-рескейл — на loop-потоке.
- **Ввод в физ. пикселях** приходит от ОС → делить на `DpiScale` до хит-теста (layout в DIP).
- **Кастомный NC** — весь хардкод рамок/кнопок в device-px пересчитать; PMv2 авто-масштабит только *стандартный* NC.
- **Per-window, не per-process:** два окна на разных мониторах = два `DpiScale`. Никакого глобального scale.
- **Порядок операций на DPI-change:** rect → пересоздание/ресайз presenter (физ.) → инвалидация геометрии → перелейаут → кадр. Всё через очередь, в один Update.
- **Суб-аллокатор** (`DeviceMemoryAllocator`) уже снимает риск OOM при частых пересозданиях буферов на ре-тесселяции.

---

## 6. Развилки (решено)

1. **Модель — DIP / логические пиксели как в WPF.** (Альтернатива «физика + ×scale трансформ на корне» отклонена — грязнее, два смысла масштаба.)
2. **Awareness — через API** (`SetProcessDpiAwarenessContext`), не манифест (гибче, гейт под ОС).
3. **Порядок — Ф0-2 (фундамент+масштаб) → Ф3 (геометрия).**
4. **Обводки — GPU, только прокинуть scale.** Геометрия тел — **не «просто доскейлить»** (скейл сетки множит ошибку хорды → фасетки на безье/крупных дугах): вся тесселяция становится **device-scale-aware** (Ф3), плотность от сагитты в device-px, цель — незаметность артефактов на любом масштабе. Триггер ре-флаттеринга (на смену scale / под max-scale / бакеты) — открыт внутри Ф3.
5. **`DpiScale` — `Vector2` (X/Y), не скаляр** — future-proof под анизотропные устройства (растянутые режимы, неквадратные панели, телефон/планшет; API даёт `dpiX/dpiY`, а в движке анизотропия уже есть через трансформы). Скалярные потребители (плотность тесселяции, фриндж, ширина обводки) схлопывают через `max(X,Y)`; viewport/проекция — по осям; SDF-эллипс — нативно.
6. **SDF-семейство вместо тесселяции для примитивов.** Эллипс/круг → SDF-батч (сиблинг `RectBatch`); настоящий эллипс rx≠ry рисуется точно (изолиния `length(local/half)−1` + `fwidth` AA — паритет с rounded-rect). Fallback (сектор/равномерная обводка/поворот/градиент) → тесселяция. Направление: capsule/ring/regular-n-gon/line тоже SDF-абельны. Не строго DPI (помогает и на 1×), но делается **до/параллельно Ф3** и сокращает её объём.

---

## 7. Статус

Дизайн согласован (2026-07-04). Реализация — по фазам, начиная с Ф0-2.
