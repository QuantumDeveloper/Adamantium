# Logical vs Visual Tree — единый дизайн

Статус: **ЧЕРНОВИК НА СОГЛАСОВАНИЕ.** Код не трогаем, пока не договоримся по этому документу.
Дата: 2026-07-26. Повод: `{Ancestor …, Logical=True}` из содержимого элемента ItemsControl не доходит
до `ItemsControl` — логический walk упирается в template-границу. Копнули — оказалось, парентинг делается
ситуативно в разных местах. Нужен один чёткий закон вместо полукостылей.

---

## 0. TL;DR (итог)

Аксиома движка **уже правильная** и совпадает с WPF/Avalonia:

> **Визуальное дерево — полное и непрерывное** (layout, render, hit-test, routed-события, фокус, ancestor-бинды).
> **Логическое дерево — «мелкое», только для наследования значений (DataContext/Inherits), ресурсов и control-триггеров.**

Все механизмы для этого в движке **уже есть**. Единственный настоящий дефект: **логические walk'и не переходят
границу шаблона через `TemplatedParent`**, поэтому обрываются на корнях template-островов. Фикс = научить
логический обход мосту `TemplatedParent` (ровно как `LogicalTreeHelper` в WPF), плюс убрать накопившиеся
несогласованности. Ни реструктуризации контейнеров, ни ломки наследования не требуется.

---

## 1. Единый закон (целевая модель)

Один элемент — один рантайм-объект, но два **представления-обхода**:

| | В ВИЗУАЛЬНОМ дереве? | В ЛОГИЧЕСКОМ дереве? |
|---|---|---|
| Объявленный/сгенерённый контент (Border в разметке, DataTemplate-контент) | да | да |
| Сгенерённый контейнер (ListBoxItem/TreeViewItem) | да | да |
| Template-внутренности (части ControlTemplate: ContentPresenter, ItemsPresenter, chrome-Border) | да | **нет** — только визуал, мост назад через `TemplatedParent` |
| Popup.Child / Slider tooltip / ContextMenu (рендерятся на overlay) | нет | да (чтобы наследовать DataContext и темиться) |
| Behavior (не-визуальный помощник) | нет | **да** (Avalonia-модель) — чтобы наследовать DataContext и резолвить бинды |

**Кто по какому дереву ходит (закон, без исключений):**

| Потребитель | Дерево | Обоснование |
|---|---|---|
| Layout (measure/arrange, invalidation) | **Визуал** | Полное дерево; логический loop дважды мерял бы templated-контент |
| Hit-test | **Визуал** | |
| Routed-события (bubble/tunnel), ContextMenu-owner | **Визуал** | `ObservableParent == VisualParent` |
| Focus | **Визуал** | Кликнутый template-part должен резолвиться в свой контрол |
| Render (`RenderParent`, paint order) | **Визуал** (через `RenderParent`, переопределяемый — адорнеры/попапы) | |
| `{Ancestor}` (дефолт), `{Binding ElementName}`, поиск по Name | **Визуал** | Единственное непрерывное дерево, пересекает шаблоны — как FindAncestor в WPF **и** Avalonia |
| Наследование значений (DataContext, Inherits-свойства) | **Логическое** (+ мост `InheritanceParent` через template-границу) | |
| Ресурсы (`FindResource`) | **Логическое → визуал как fallback** | Локальный ресурс из логической области виден templated-контенту |
| Control-триггеры (scoping) | **Логическое** | |
| `{Ancestor …, Logical=True}` (opt-in) | **Логическое, с мостом `TemplatedParent`** | «Обход по дереву контента» — см. §4 |
| Style-селекторы (Match/BasedOn/DefaultStyleKey) | **Ни то ни другое — цепочка ТИПОВ** (`BaseType`) | |
| Style-инвалидация (re-theme) | **Оба** (общий visited-set) | Template-part'ы визуальны, не логичны |

Это ровно то, что делают WPF и Avalonia (см. §5). Отличие только в удобстве: наш `Logical=True` будет
**мостить template-острова через `TemplatedParent`** (полезнее сырого логического родителя WPF, который на
корне острова просто отдаёт null).

---

## 2. Текущая реальность (карта по аудиту)

### 2.1 Ядро парентинга
- `FundamentalUIComponent.SetParent` (единственная точка, `FundamentalUIComponent.cs:372-410`): ставит
  `parent` + `InheritanceParent = logicalParent` (стр. 392), на аттаче зовёт `ApplyCurrentTheme()` +
  `OnAttachedToLogicalTree` (событие `AttachedToLogicalTree`). Публичного `SetParent` нет — только через
  `AddLogicalChild`/`RemoveLogicalChild` → `LogicalChildrenCollection`.
- Визуальная сторона (`UIComponent.AddVisualChild`/`SetVisualParent`, `UIComponent.cs:638-683`) **никогда не
  трогает `InheritanceParent` и логического `parent`**.
- **Template-граница** (`TemplatedUIComponent.AddTemplateChild`, `:119-138`): `InheritanceParent = this` +
  `AddVisualChild` + `ApplyCurrentTheme()` на корне — но **`SetParent`/`AddLogicalChild` НЕ зовётся**. Отсюда
  весь ручной мирроринг (тема+наследование дублируют то, что для логических детей делает `SetParent`).
- `ControlTemplate.Build` (`ControlTemplate.cs:23-68`): **проставляет `TemplatedParent` на КАЖДУЮ часть**
  шаблона (обход субтри). ⇒ мост уже существует, просто им не пользуются логические walk'и.

### 2.2 Где парентят логически (13 мест)
ContentPresenter (контент → сам себе, `:195`), ContentControl (только untemplated-путь, `:173`),
Decorator/Border (лог.+визуал, `:32`), Panel (каждый Child лог.+визуал, `:41`), ItemsPresenter (панель →
себе, `:47`), VirtualizingPanel/WrapPanel (контейнер → себе, `:253`/`:71`), Popup.Child (только лог., `:201`),
ContextMenu (`InputUIComponent.cs:187`), Slider tooltip (только лог., `:340`), TabStripScroller (`:130`),
TextBlock inlines (`:242`), Behavior (`Behavior.cs:19`).

### 2.3 Два «логических острова» (корень бага)
От DataTemplate-Border вверх по `LogicalParent`:
```
Border(item) → ContentPresenter контейнера → [корень ControlTemplate контейнера] → null   ⛔ остров #1
```
Отдельно, контейнеры парентятся к ПАНЕЛИ, не к ItemsControl:
```
ListBoxItem → панель → ItemsPresenter → null   ⛔ остров #2
```
Оба обрыва — на template-part'ах (у которых `LogicalParent == null`, но `TemplatedParent` **проставлен**).
Наследование через провал живёт (через `InheritanceParent`), поэтому `{Binding}` DataContext работает —
а `LogicalParent`-обход рвётся. Именно поэтому `ListBoxItem.OwnerListBox` (`:130`) специально ходит по
ВИЗУАЛИ (`GetVisualAncestors`), а не логически.

### 2.4 Мост через острова уже собирается, если добавить `TemplatedParent`:
```
Border(item) → ContentPresenter → [null? → TemplatedParent] ListBoxItem → панель → ItemsPresenter
             → [null? → TemplatedParent] ItemsControl   ✅
```

---

## 3. Несогласованности, которые чистим заодно

1. **Логические walk'и написаны инлайн и по-разному.** Нет хелпера `GetLogicalAncestors`; логический обход
   продублирован в `AncestorBindingExpression` (логическая ветка), `ResourceManager.LogicalOrVisualParent`,
   `FundamentalUIComponent.InvalidateStylesCore`, наследовании. **Фикс:** один хелпер
   `GetLogicalAncestors()`/`GetLogicalParent()` с мостом `TemplatedParent`, все инлайны → на него.
2. **`SetParent` молча репарентит** вместо throw при непустом старом родителе (`:378-387`). Это осознанный
   хак под ре-тему. **Решение:** оставить, но задокументировать как намеренное (не «баг»).
3. **ContentPresenter парентит контент сам к себе**, ContentControl — только в untemplated-пути. Два хоста
   контента с чуть разной проводкой. **Решение:** оставить (templated ContentControl делегирует своему
   ContentPresenter-part'у — это ок при мосте `TemplatedParent`), задокументировать разделение ролей.
4. **`InvalidateStylesCore` ходит по обоим деревьям** — это КОРРЕКТНО (template-part'ы визуальны), не трогаем,
   просто фиксируем в законе.
5. **Адорнеры не парентят логически вообще.** Проверить, нужно ли им наследование DataContext (для
   темизированных адорнеров-индикаторов — да). **Решение:** обсудить в §6, возможно привести к модели
   Popup (логический ребёнок без визуального).

---

## 4. Что делаем с `{Ancestor …, Logical=True}` (твой исходный вопрос)

Твоя интуиция «`Logical=True` → идём по логическому родителю → должны дойти до ItemsControl» —
**правильная по духу**, ломалось только из-за обрыва на template-острове.

**Предложение:** логический обход (в т.ч. `Logical=True`) идёт по
`LogicalParent`, а на template-корне (где `LogicalParent == null`, но есть `TemplatedParent`) **перескакивает
через `TemplatedParent`**. Тогда `Logical=True` честно доходит до `ItemsControl` по дереву контента/контейнеров.
Это ровно семантика `LogicalTreeHelper` в WPF (логическое дерево + мост через `TemplatedParent`), только у нас
мост зашит в сам обход, чтобы им было удобно пользоваться.

При этом **дефолтный `{Ancestor}` остаётся визуальным** (первичный, всегда-работающий путь — как FindAncestor
в WPF и Avalonia). `Logical=True` — осознанный opt-in «по дереву контента» (аналог `$parent` /
`FindLogicalAncestorOfType` в Avalonia).

Мой недавний фикс behavior→visual-от-хоста этому НЕ противоречит: он про дефолтный визуальный путь и
остаётся. Behavior — логический ребёнок хоста (Avalonia-модель), и его дефолтный `{Ancestor}` резолвится по
визуали хоста; а `Logical=True` от behavior'а пойдёт по логике хоста с мостом `TemplatedParent`.

---

## 5. Ориентир: WPF и Avalonia (кратко)

- **Оба** гоняют `RelativeSource FindAncestor` по **визуальному** дереву (оно непрерывно, пересекает шаблоны).
- **Оба** держат наследование (DataContext/inherited) и ресурсы на **логическом** дереве.
- **Оба**: сгенерённые контейнеры — в логическом дереве; template-внутренности — только визуал, мост
  `TemplatedParent`.
- **Avalonia**: behavior'ы (`Xaml.Interactivity`) **вводятся в логическое дерево** через `ISetLogicalParent`,
  чтобы наследовать DataContext. WPF — через `InheritanceContext` (Freezable-костыль). **Мы берём модель
  Avalonia** (уже сделано: `Behavior : FundamentalUIComponent` + `AddLogicalChild`).
- Avalonia даёт оба обхода явными именами: `FindAncestorOfType` (визуал) vs `FindLogicalAncestorOfType`
  (логика). Наш аналог — дефолт vs `Logical=True`.

---

## 6. План миграции (инкрементальный, низкорисковый)

Каждый шаг самостоятелен, компилируется и проверяется отдельно. Порядок — от чистого выигрыша к спорному.

- **Шаг 1 — мост `TemplatedParent` в логическом обходе (закрывает исходный баг).**
  Ввести `IFundamentalUIComponent.GetLogicalParentOrBridge()` = `LogicalParent ?? (this as ITemplatedComponent)?.TemplatedParent`
  и хелпер `GetLogicalAncestors()`. Перевести логическую ветку `AncestorBindingExpression.FindAncestor` на него.
  ⇒ `{Ancestor ItemsControl, Logical=True}` из item'а доходит до ItemsControl.
  Проверка: тест-бинд из DataTemplate + behavior из демо DnD.

- **Шаг 2 — унифицировать остальные логические walk'и** на тот же хелпер: `ResourceManager.LogicalOrVisualParent`
  (мост даст ресурсам дойти по логике до ItemsControl-области), `InvalidateStylesCore` (аккуратно — там уже и
  визуальный проход; не задвоить). Проверка: ресурс, объявленный на ItemsControl, виден из item-контента.

- **Шаг 3 — задокументировать закон в коде** (короткие ссылки на этот док в `SetParent`, `AddTemplateChild`,
  `AncestorBindingExpression`, `ResourceManager`) и пометить намеренные исключения (silent-reparent,
  ContentPresenter-self-parent) как «by design», а не «TODO/hack».

- **Шаг 4 — адорнеры: наследование через `InheritanceParent` (НЕ полный логический ребёнок).** `Adorner.AdornedElement`
  setter ставит `InheritanceParent = AdornedElement` → адорнер наследует DataContext/inherited-значения. **ВАЖНО —
  почему НЕ `AddLogicalChild` (изначально выбранная «модель Popup»):** адорнер — это framework-chrome, темизируемый
  ВНЕ дерева адорнер-стадией (`AdornerRenderProcessor`, флаг `ThemeApplied` + `manager.ApplyTheme`), ровно как
  template-root. Полный `AddLogicalChild` дёргает `SetParent → ApplyCurrentTheme` на КАЖДОМ создании адорнера
  (hover/selection/drop-cue), пересобирая его шаблон и **пере-подписывая его `{ThemeResource}` на глобальный `Theme`**;
  а логический detach НЕ фичит `Unloaded`, поэтому `ThemeResourceExpression` не закрывался → подписки на `Theme`
  копились → каждая смена акцента дёргала все накопленные → прогрессирующий лаг колор-пикера. `InheritanceParent`
  даёт наследование БЕЗ темизации (тема остаётся за стадией) — та же модель, что у template-root'а в `AddTemplateChild`.
  Это ОТКЛОНЕНИЕ от буквального ответа юзера «модель Popup», сделано ради корректности+перфа (адорнер = chrome, не
  user-content).

- **Шаг 5 (опционально) — cleanup хелперов:** `GetVisualAncestors`/`GetLogicalAncestors` симметричной парой в
  `UIExtensions`, чтобы будущие потребители не писали инлайн-циклы.

**НЕ делаем:** не превращаем контейнеры в прямых логических детей ItemsControl (не нужно — мост
`TemplatedParent` закрывает разрыв панель→ItemsPresenter→ItemsControl), не трогаем layout (остаётся
визуал-only), не трогаем push-модель наследования.

---

## 7. Жёсткие инварианты (что редизайн ОБЯЗАН сохранить)

Из аудита потребителей — редизайн не должен их сломать:
1. Layout/hit-test/routing/focus/render — **визуал-only**, пересекают шаблоны.
2. Наследование идёт от `LogicalParent`, **пушится** (не pull), order-independent, локальные значения
   побеждают, и **доходит до template-контента** (через `InheritanceParent`).
3. `{Ancestor}` — dual-mode (визуал-дефолт / логик-opt-in) + пере-резолв на attach/detach нужного дерева +
   не-визуальный (Behavior) кейс от хоста. Смена родителя обязана поднимать attach/detach-события.
4. Route == визуальная цепочка (`ObservableParent`).
5. `RenderParent` — отдельный переопределяемый хук (адорнеры/попапы рисуют вне визуального родителя).
6. Ресурсы — логика→визуал fallback; обе ссылки должны быть доступны с узла.
7. Style-матч — по типу; style-инвалидация — по обоим деревьям с visited-set.
8. Поиск по Name — по визуальным детям (включая template-part'ы).
9. Чёткий attach/detach-переход на дерево, поднимающий события; `RootVisual` достижим по визуали.

---

## 8. Открытые вопросы к согласованию

1. **Мост `TemplatedParent` в логическом обходе** (§4) — принимаем как основное решение? (моя рекомендация: да)
2. **Дефолт `{Ancestor}` — визуальный, `Logical=True` — логический-с-мостом.** Ок? (совпадает с WPF/Avalonia)
3. **Адорнеры** (§6 шаг 4) — приводим к логической модели Popup или оставляем overlay-only? (нужен твой ввод)
4. Объём: делаем весь план (шаги 1–5) или только шаг 1 (закрыть баг) + документ, а остальное позже?
