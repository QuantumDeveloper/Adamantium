# Текст: батчинг глифов из общего атласа (рендеринг коллекций) — анализ и план

_Статический анализ + ЗАМЕР (Release, dev Quadro RTX 4000). Незакоммиченный артефакт (не в индексе). Ссылки на код — `файл:строка`._
_v1 — повод: FPS-регресс на `ListBox` (Stack- и Wrap-виртуализация) до ~30. Замер показал: узкое место — НЕ растеризация текста и НЕ аналитическое AA, а **CPU-запись композитов** (194 draw'а/кадр). Решение — собирать текст коллекций в ОДИН инстансный draw из общего MSDF-атласа прямо в основной проход. FontRenderer уже инстансовый — это эволюция, не пайплайн с нуля._
_v2 (ПОПРАВКА пути): активный per-draw путь — **`EffectPass.ApplyHeap` (`EffectPass.cs:138`), descriptor-HEAP**, а НЕ `ApplyBuffer` (`UseDescriptorHeap=true` — рантайм-дефолт; buffer только в Designer.Host, см. [[descriptor-heap-driver-limitation]]). `ApplyHeap` **легче**: без `MapMemory`/`UnmapMemory`, offset'ы текстур/сэмплеров предвычислены линкером (`GlobalHeapOffset`), CB по BDA-адресу. Per-draw стоимость там = CB `Allocate`+`CopyFrom`+`GetDeviceAddress`, `BindShader` на стадию (vkCmdBindShadersEXT), `PushDataEXT`, + строковые лукапы `Effect.Parameters[name]`/`parameterPushOffsets`. Тезис плана НЕ меняется (схлопывание N draw'ов → ~1), меняется только адрес узкого места. Ссылки на `ApplyBuffer`/`§6 Фаза 0` ниже читать с этой поправкой._
_v3 (РЕШЕНИЕ по реализации, 2026-06-30): in-shader пер-блочная матрица (BlockData/blockId из §4-6) **НЕВОЗМОЖНА на dev-Turing — ЛЮБОЙ непрямой доступ к mat4 в графическом шейдере 100%-AVs `vkCreateShadersEXT`** (проверено 3 раза: BDA mat4 load, BDA 4×float4, CB-массив `float4x4[256]` по пер-инстанс индексу). Выживает только ОДИН статический `float4x4`-юниформ (baseline; FillFringe/Stroke так и делают — статический `Projection`, а BDA только в compute). compute тоже отвергли (чтение mat4 по BDA в compute не проверено + крупно). → **Принят CPU-пред­трансформ (§9)**: позиции глифов запекаются на CPU, VS жмёт только статический `Projection`. §4-6 (in-shader lookup) — SUPERSEDED §9; вернуться к ним, когда драйвер починят. FontEffect.fx переписан на Slang (это ОК, работает) и оставлен._

---

## 0. Кратко (TL;DR)

- **Замерено, в чём боль.** Кадр ~33 мс, из них **`drawLoopCPU` ≈ 25 мс** (76%) — это CPU-запись цикла отрисовки. `unitDraws/frame = 194`, `textRasters/frame = 0.1`. Вывод: упираемся не в GPU/презент и не в растеризацию текста (она закэширована), а в **per-draw стоимость записи 194 юнитов** ≈ 0.13 мс CPU на draw. Лог замера — §2.
- **Где именно тратится.** `unit.Render()` → `EffectPass.ApplyBuffer` (`EffectPass.cs:259`) на КАЖДЫЙ юнит КАЖДЫЙ кадр: суб-аллокация пула + копия constant buffer + записи дескрипторов (uniform/texture/sampler) + `BindDescriptors` + `BindShader`. Плюс per-draw аллокации (`new DescriptorBufferBindingInfoEXT[...]`) и запрос **константного** layout-offset'а на каждый ресурс каждого draw'а.
- **Айтемы коллекций — это в основном текст.** Сейчас каждый текст-блок = приватная RT (`mesh.Bounds × TextSupersample`) → растеризуется в неё (закэшировано) → **композитится** в основной проход отдельным draw'ом. N айтемов = N композитов = основная масса тех 194 draw'ов.
- **FontRenderer УЖЕ инстансовый.** `GraphicsDevice.Draw(4, layout.ElementsCount)` (`FontRenderer.cs:203`) — 4 strip-вершины × число глифов, **один инстанс на глиф**, пер-глифовые данные в `layout.VertexBuffer`, сэмплинг общего MSDF-атласа (`layout.FontAtlas.Atlas`, `:171`), пассы `FontBatchRender...` (`:192-200`), глифы в **локальных** координатах + внешний трансформ `finalMatrix = transformMatrix × ortho` (`:157`, `:172`). То есть один блок уже рисуется одним инстансным draw'ом.
- **Цель.** Поднять инстансинг с уровня «один блок» до «вся коллекция»: один draw на N блоков, прямо в основной проход, без пер-блочных RT. Это снимает 25 мс (N композитов → ~1 батч), а сверху даёт: **экономию VRAM** (N супер-сэмплированных RT уходят), **отсутствие ре-растеризации на скролле** (recycle айтема больше не растеризует RT), и **единый проход** без прерывания (сегодня растеризация в RT рвёт основной проход — §1).
- **«Прокачка» FontRenderer — 4 дельты** (§4): (1) трансформ из per-draw юниформа → пер-инстанс по `blockId`; (2) сводный инстанс-буфер видимых блоков; (3) вход в основной проход без `SetState/RestoreState`; (4) клип-rect блока в шейдере.
- **Общая инфра.** Пер-блочный storage-буфер `{transform, clipRect, color/opacity}`, индексируемый по инстансу (через BDA) — **один и тот же** для текст-батча и для инстансинга фигур (фоны/бордеры). Строим раз — служит обоим.
- **Интерим почти бесплатно** (§6, Фаза 0): закэшировать константные layout-offset'ы + убрать per-draw аллокации в `ApplyBuffer`. Снимает несколько мс с ЛЮБОГО рендера, до большой стройки.

---

## 1. Карта архитектуры (текущий поток текста)

```
TextBlock.OnRender(IDrawingContext)                                 TextBlock.cs:220
  → context.ForControl(this).DrawText(GetTextRenderingParameters(), DesiredSize, _textLayout, …)   :223
      → текст-RenderUnit (RenderUnit.cs)
          PreRender (pre-pass, ДО основного прохода):
            FontRenderer.SetState(sampler, translation, privateRT)  FontRenderer.cs:98
                → EndDraw() основного прохода (если активен)         :121   ← ПРЕРЫВАНИЕ
                → SetRenderTargets(privateRT) + BeginRendering        :128-132
            FontRenderer.DrawLayout(layout, fg, stroke)              :93 → DrawInternal :137
                effectTexture := layout.FontAtlas.Atlas              :171
                effectMatrixTransform := finalMatrix (translation×ortho)  :157,:172
                SetVertexBuffer(layout.VertexBuffer)                 :185
                Draw(4, layout.ElementsCount)   // ИНСТАНСНО          :203
            FontRenderer.RestoreState()                              :206 → возврат RT/scissor/viewport
          (растеризация кэшируется флагом _textRendered — повтор только при изменении)

  основной проход:
    RenderCache.Render(device, fullScissor)                         RenderCache.cs:120
      foreach unit in _renderUnits:                                 :123
        unit.Update(wt, proj, scale)                                :152
        unit.Render()                                               :154
          текст-композит: квад с привязанной privateRT-текстурой,
          построен как Rect(-pad,-pad, ds.W+2pad, ds.H+2pad)        RenderUnit.cs:496
          → EffectPass.ApplyBuffer (дескрипторы + бинд + draw)      EffectPass.cs:259
```

**Два следствия текущей схемы, которые и лечим:**
1. **N композитов в основном проходе** (по композиту на блок) = N × `ApplyBuffer` = замеренные 25 мс CPU.
2. **Прерывание прохода под растеризацию.** В Vulkan render pass'ы не вкладываются, поэтому растеризовать блок в его RT можно только прервав основной проход (`EndDraw` `:121`) либо собрав все растры в pre-pass ДО основного — отсюда вся логика `SetState/RestoreState` со снимком viewport/scissor. В целевой схеме растеризации-в-RT внутри кадра нет → проход один, непрерывный.

---

## 2. Замер (обоснование)

Лог `C:\Temp\adamantium_perf.log`, `ListBox` (Stack+Wrap, виртуализация) на экране, установившийся кадр:

```
fps=  30.0  frame= 33.36ms  drawLoopCPU= 25.05ms  unitDraws/frame= 194.0  textRasters/frame= 0.1
fps=  30.5  frame= 32.83ms  drawLoopCPU= 24.38ms  unitDraws/frame= 194.0  textRasters/frame= 0.1
…
```

- `drawLoopCPU` ≈ 25 мс из 33 мс кадра → **CPU-bound в цикле отрисовки**. Остаток ~8 мс — сборка кэша, презент, vsync.
- `textRasters/frame = 0.1` → растеризация текста переиспользуется, **не** виновата.
- `AnalyticAntialiasing=false` → всего +5 FPS → fringe AA тоже не основной вклад.
- 25 мс / 194 draw'а ≈ **0.13 мс CPU на draw** — много для записи одного draw'а; пахнет аллокациями/маршаллингом и повторением константной работы на каждый вызов (см. `ApplyBuffer`).

> Инструментация (`RuntimeStats.TextRasterCount/RenderUnitDrawCount/RenderLoopMs`, таймер в `RenderCache.Render`, `WindowRenderService.PerfProbeLog`) — **TEMP**, снять по завершении (Фаза 6).

---

## 3. Что уже готово (переиспользуем как есть)

| Возможность | Где | Статус |
|---|---|---|
| Инстансный глиф-квад (4 strip-верш. из `SV_VertexID`, 1 инстанс/глиф) | `FontRenderer.cs:186-187,:203` | ✅ |
| Пер-глифовые данные (UV в атласе, локальная позиция, метрики) | `layout.VertexBuffer` (`:185`) | ✅ |
| Общий MSDF-атлас как сэмплируемый вход | `layout.FontAtlas.Atlas` (`:171`) | ✅ |
| Эффект-пассы батч-рендера (msdf / canonical / outline / stroked) | `FontBatchRender*` (`:192-200`) | ✅ |
| Глифы в ЛОКАЛЬНЫХ координатах + ВНЕШНИЙ трансформ | `finalMatrix` (`:157,:172`) | ✅ ← ключ к батчу |
| Кэш растеризации/геометрии по dirty | `_textRendered`, `layout` кэш | ✅ |

Вывод: тяжёлое (растеризация глифов из MSDF-атласа инстансингом, эффект, локальная геометрия) — сделано. Не хватает только подъёма с «одного блока за draw» до «многих блоков за draw» + входа в основной проход.

---

## 4. Дельты FontRenderer («прокачка»)

### 4.1 Трансформ: per-draw юниформ → пер-инстанс по индексу
Сейчас `effectMatrixTransform.SetValue(finalMatrix)` — ОДИН трансформ на весь draw ⇒ один блок за вызов. Делаем:
- каждый глиф-инстанс несёт **`blockId`** (расширить пер-глифовую структуру либо параллельный поток инстанс-атрибутов);
- шейдер `FontEffect.fx` читает `blockData[blockId].transform` из storage-буфера вместо юниформа;
- проекция остаётся глобальной (ortho основного прохода), задаётся раз на батч.

### 4.2 Сводный инстанс-буфер по блокам
Сейчас `VertexBuffer` на блок. Для одного draw'а — **один сводный инстанс-буфер** видимых блоков + `blockId` на инстанс. Пер-блочные `transform/clip/color` — в маленьком storage-буфере, индексируемом `blockId` (Фаза 1, BDA).
- **Dirty-гранулярность не меняется:** скролл → обновляем только массив `blockData` (трансформы); смена текста/recycle айтема → до-собираем сводный инстанс-буфер (та же логика, что пере-сборка `VertexBuffer` сейчас).

### 4.3 Вход в основной проход (без прерывания)
Новый метод (напр. `RecordBatch(...)` / `AppendToMainPass(...)`): пишет инстансный draw в ТЕКУЩИЙ проход — **без** `SetState/RestoreState`, без смены RT, без сброса viewport/scissor на RT, с проекцией основного прохода. `DrawInternal` (`:137`) переиспользуется почти весь; меняется источник трансформа (4.1) и убирается RT-возня.

### 4.4 Клип-rect блока в шейдере
`FontEffect.fx` дискардит фрагмент вне `blockData[blockId].clipRect`. Это индивидуальный клип айтема (текст вылез за свой айтем). Viewport-клип (общий по `ScrollViewer`) остаётся **одним** scissor на весь батч. Тримминг (ellipsis `…`) сюда НЕ относится — он часть шейпинга (`TextLayout.ProcessText`) и запекается в набор глиф-инстансов одинаково в обоих путях.

---

## 5. Общая инфраструктура пер-инстанс данных (текст + фигуры)

Один storage-буфер на кадр (через BDA), индексируемый по инстансу:
```
struct BlockData {           // на блок текста ИЛИ на фигуру
    float4x4 transform;      // блок/элемент → мир (локальная геометрия остаётся статичной)
    float4   clipRect;       // x,y,w,h в координатах основного прохода (для discard)
    float4   color;          // foreground / fill; opacity в .w
    // (опц.) stroke, флаги
}
```
- **Текст:** `blockId` глифа → `BlockData` блока (§4).
- **Фигуры (фоны/бордеры/галки):** одна quad-геометрия, `instanceId` → `BlockData` (трансформ+цвет). Один инстансный draw на пачку однотипных фигур.
- Строится один раз — обслуживает оба пути. Это и есть «инстансинг коллекций» целиком.

---

## 6. План по фазам

### Фаза 0 — Интерим (опционально; на heap-пути выигрыш СКРОМНЕЕ)
- Активный путь — `ApplyHeap`, в нём НЕТ `MapMemory`/`UnmapMemory` (главная боль `ApplyBuffer` неприменима). Дешёвое, что есть: **закэшировать строковые лукапы** `Effect.Parameters[links....Name]` и `parameterPushOffsets[..]` на самом link'е (константны на link, сейчас словарный лукап по строке на каждый ресурс каждого draw'а). `BindShader`/`PushDataEXT`/CB-копия — неустранимы пер-draw, их снимает только батч (Фазы 1+).
- **Verify:** при тех же `unitDraws=194` `drawLoopCPU` падает на сколько-то мс; визуально без изменений; 215 тестов зелёные.
- **Вывод:** heap-путь и так лёгкий → основной выигрыш в батче (схлопывание draw'ов), а не в Фазе 0. Разумно сразу к Фазе 1.

### Фаза 1 — Пер-инстанс storage-буфер + шейдерный вход (фундамент, §5)
- Завести `BlockData`-буфер (BDA), заполнение на кадр, шейдерный доступ по индексу.
- **Verify:** микротест — один прямоугольник, трансформ из буфера по индексу, рисуется корректно.

### Фаза 2 — FontRenderer: transform per-instance + вход в основной проход (§4.1, 4.3)
- `blockId` в глиф-инстансе; `FontEffect.fx` берёт трансформ из `BlockData`; новый `RecordBatch` без RT-прерывания.
- **Verify:** ОДИН текст-блок рисуется в основном проходе одним draw'ом без приватной RT, **визуально идентично** старому пути (скрин-сравнение на GPU).

### Фаза 3 — Агрегация по блокам (один draw на коллекцию) (§4.2)
- Сводный инстанс-буфер видимых блоков + `blockId`; проход в `RenderCache`, собирающий смежные текст-юниты в батч и зовущий FontRenderer один раз; dirty: скролл → только `BlockData`, membership → пере-сборка буфера.
- **Verify:** `ListBox` на N айтемов → текст-`unitDraws` схлопывается с ~N до ~1; `drawLoopCPU`↓, FPS↑ (цель: вернуть 120). Лог замера до/после.

### Фаза 4 — Клип в шейдере (§4.4)
- `clipRect` в `BlockData`; discard в `FontEffect.fx`; viewport — один scissor на батч.
- **Verify:** текст, вылезающий за айтем, режется по границе айтема ВНУТРИ одного батч-draw; ScrollViewer-обрезка краевых айтемов корректна.

### Фаза 5 — Инстансинг фигур (тот же буфер, §5)
- Фоны/бордеры айтемов: одна quad-геометрия + `instanceId` → `BlockData`.
- **Verify:** фоны айтемов схлопываются в один инстансный draw; общий `unitDraws/frame` на `ListBox` кратно падает.

### Фаза 6 — Уборка
- Убрать пер-блочные text-RT и (если больше не нужен) путь `SetState/RestoreState` + text pre-pass.
- Снять TEMP-инструментацию (`RuntimeStats` probes, таймер `RenderCache.Render`, `WindowRenderService.PerfProbeLog`).
- **Verify:** VRAM на `ListBox` ниже (нет N RT); один проход; полный прогон тестов.

---

## 7. Открытые вопросы / риски (проверить до/во время Фазы 2-3)

1. **Резидентность атласа.** `layout.FontAtlas` — атлас **на layout** или общий на (шрифт×размер)? Для одного батч-draw'а, сэмплящего ОДИН атлас, все собранные глифы должны лежать в одном атласе. Если атлас на (шрифт×размер) — батчим **по атласу** (группировка по `FontAtlas`); список со смешанными шрифтами/размерами даст несколько батчей (всё равно ≪ N). Проверить устройство `FontAtlas` и стратегию резидентности глифов видимого текста.
2. **MSAA основного прохода.** Сейчас text-RT без MSAA (MSDF само-AA-ится). В основном проходе текст пишется в MSAA-таргет — убедиться, что MSDF + MSAA не дают артефактов по краю квадов (ожидаемо ок: кромка глифа в шейдере, не на геометрии квада).
3. **Порядок отрисовки / z.** Батч текста должен лечь поверх фонов айтемов и в правильном слое относительно прочего контента. Батчим только СМЕЖНЫЕ одно-слойные текст-юниты; перемежение с не-текстом на другом z → разрыв батча (айтемы списка идут подряд → ложится хорошо).
4. **Stroked-текст** (`FontBatchStrokedTextPass`) — `strokeColor` тоже уносим в `BlockData`.
5. **Subpixel-режим** vs клип-discard и премультипликация — проверить на Фазе 4.
6. **Одиночный статичный текст оставляем на старом пути?** Пер-блочная RT оптимальна для изолированного статичного текста (раз растеризовали — дёшево композитим). Решить: батч включаем для коллекций/много-блочных сцен, а одиночный текст может остаться на RT — или унифицировать всё на батч (проще поддерживать, но переписать больше). Дефолт предложения: унифицировать на батч, RT-путь удалить (Фаза 6), если §7.1 и §7.2 без сюрпризов.

---

## 8. Связанные документы
- `ITEMSCONTROL_VIRTUALIZATION_PLAN.md` — виртуализация уже выкидывает невидимые айтемы; батч получает только видимые+краевые.
- `GPU_BUFFER_REUSE_PLAN.md` — `ReusableBuffer`/ring-пул; сводный инстанс-буфер ложится на ту же модель.
- `TRIANGULATION_STROKE_GPU_PLAN.md` — GPU-линии; пер-инстанс `BlockData` потенциально общий и для обводок.

---

## 9. РЕАЛИЗАЦИЯ: CPU-пред­трансформ (РЕШЕНО 2026-06-30, supersedes §4-6)

Драйверо-безопасный путь: пер-блочный трансформ применяется к позициям глифов **на CPU** (общий случай — полная матрица), графический VS жмёт **только статический `Projection`** (= паттерн FillFringe/Stroke; единственная конструкция, которая не AV'ит). НЕ требует BDA/массивов/индексов матриц в шейдере.

**ВАЖНО — проверять на GPU (юзер делает на пробуждении), вслепую НЕ строилось.** Текущее состояние дерева: рабочий baseline (юниформ `MatrixTransform`) + Slang-перепись FontEffect.fx, собирается чисто. TEMP-перф-инструментация на месте (для замера выигрыша; снять в конце).

### Матрица (вывод)
Сейчас раст­р в RT: `finalMatrix = Translation(location) × RT_ortho`, где `location = TextArea.X/Y + pad` (UIRenderComponent.cs:336); глифы из `layout.VertexBuffer` — в layout-локальных координатах. Композит: RT-квад `Rect(-pad,-pad, ds+2pad)` рисуется при `WorldTransform` (`RenderData.TransformMatrix`).
⇒ глиф mesh-pos = `glyph_local + TextArea` (pad сокращается: RT-origin -pad, location +pad). ⇒ для ПРЯМОГО draw (row-vector конвенция движка):
```
mvp = Translation(TextArea.X, TextArea.Y, 5)  ×  RenderData.TransformMatrix  ×  RenderData.ProjectionMatrix
```
RenderScale для прямого draw = 1 (супер-сэмпл RT больше не нужен — MSDF само-AA-ится по экранным производным `fwidth`). **На GPU проверить порядок/конвенцию (row-vector + CopyMatrixColumnMajor транспонирует при заливке юниформа — `EffectParameter.cs:557`).**

### Стадия 1 — прямой draw в основном проходе (по блоку), за тоглом
БЕЗ изменения шейдера (одна статическая матрица = рабочий путь). Снимает RT + композит + прерывание прохода; проверяет «текст в основном проходе» + качество.
- Тогл `FontRenderer.UseDirectTextDraw` (static bool, default **false** — дефолт = текущий RT-путь, не трогаем).
- `FontRenderer.DrawLayoutDirect(sampler, layout, fg, stroke, Matrix4x4F mvp)`: тело как `DrawInternal` (FontRenderer.cs:137) НО без `SetState`/`RestoreState`/смены RT — ставит эффект-параметры + `MatrixTransform = mvp` + `Draw(4, layout.ElementsCount)` в ТЕКУЩИЙ проход.
  - **GPU-стейт — ВНИМАНИЕ (проверяемо только на GPU):** не переопределять viewport/scissor (берём основного прохода); **MSAA = основного таргета** (НЕ `renderTarget.MSAALevel` из `DrawInternal` — там был no-MSAA RT); **depth — как у прочих UI-юнитов основного прохода** (`UIRenderComponent.Render`: `CompareOp.Always`, test/write on — НЕ `depth off` как в RT-раст­ре); блендинг premult (как сейчас). Несовпадение стейта → текст не нарисуется/конфликт с другими draw'ами.
- Интеграция в text-юните (`TextRenderComponent`, UIRenderComponent.cs:255): при тогле — `PreRender` early-return (пропускает раст­р), `Render` early-branch зовёт `DrawLayoutDirect(mvp = Translation(TextArea)×RenderData.TransformMatrix×RenderData.ProjectionMatrix)` вместо `Texture=_renderTarget.ResolveTexture; base.Render()`. Ветки ОТДЕЛЬНО, дефолт (off) не трогать.
- **Verify (GPU):** текст на месте/верная позиция, AV нет, MSDF-качество ок без супер-сэмпла.

### Стадия 2 — агрегация (один draw на коллекцию) = выигрыш FPS
- **Шейдер:** новый VS-вход/путь `mul(position, Projection)` (статический `Projection` — безопасно); глифы приходят УЖЕ в мировых координатах. (Можно отдельным пассом FontBatch, чтобы не ломать прямой путь Стадии 1.)
- **CPU-агрегатор** (в `RenderCache` — он уже обходит юниты): собрать видимые text-юниты → ОДИН инстанс-буфер `FontItem`, у каждого глифа `Destination` запечена в мир: `Destination_world.xy = ((Destination_local.xy + TextArea) × WorldTransform)`; ширина/высота quad'а (`Destination.zw`) — домножить на масштаб из WorldTransform (для повёрнутых/масштабированных блоков корректно расширять угол уже в мире — либо запекать 4 угла, если поворот). Общий статический `Projection`. Один `Draw(4, totalGlyphs)`.
- **Атлас:** один общий → один бинд; если атласов несколько (шрифт×размер, §7.1) — группировать батчи по атласу (всё равно ≪ N).
- **Скролл/изменение:** блоки сдвинулись → пере-запечь позиции видимых глифов + пере-залить инстанс-буфер (дёшево, только видимые; ring-буфер из `GPU_BUFFER_REUSE_PLAN`). Установившийся кадр — **ноль per-frame CPU** (один draw).
- **Verify (GPU):** `ListBox` → текст-`unitDraws` с ~N до ~1; `drawLoopCPU`↓, FPS→~120 (лог TEMP-инструментации до/после).

### Клип, фигуры, уборка
- Клип айтемов: пока общий viewport-scissor (ScrollViewer). Пер-айтемный clipRect — позже (можно как пер-инстанс float4 + discard в PS; float4, не матрица → драйверо-безопасно).
- Фоны/бордеры айтемов (отдельный трек): инстансинг квадов с пер-инстанс цветом+позицией (тоже без матриц — позицию запечь/передать как float). Снимет и не-текстовые draw'ы.
- Уборка: убрать пер-блочные RT + `SetState/RestoreState` + text pre-pass; снять TEMP-инструментацию (RuntimeStats probes, таймер RenderCache.Render, WindowRenderService.PerfProbeLog).

### Шов на будущее (драйвер починят)
Заменить CPU-запечку на матрицу-в-VS (`BlockTransforms[blockId]` / BDA) — убрать пер-кадровую CPU-работу на скролле. Держать агрегатор/draw отдельно от способа трансформа, чтобы свап был точечным.
