# ItemsControl + ItemContainerGenerator + встроенная виртуализация (Stack + Wrap)

## Зачем это

В Adamantium.UI **нет** ни `ItemsControl`, ни генератора контейнеров, ни виртуализирующих
панелей — нельзя привязать коллекцию к списку сгенерированного шаблонизированного UI. Для
любого реального приложения это обязательно («без этого тупо никак»).

Цель: с нуля `ItemsControl` с `ItemContainerGenerator` и **настоящей** встроенной
виртуализацией, авторится в AUML, работает везде (рантайм + дизайнер). Виртуализация — не
тумблер «сделай медленно», а автоматическое поведение; и она **общая база** для панелей
(Stack 1D и Wrap 2D), а не отдельная панель.

ViewModel-навигация (как у Prism, встроенная) — отложена («позже ещё подумаем»); см. *Вне scope*.

## Что уже есть (переиспользуем, не изобретаем)

- `ContentPresenter.BuildCurrent` ([Adamantium.UI.Controls/ContentPresenter.cs](Adamantium/Adamantium/Adamantium.UI.Controls/ContentPresenter.cs)) уже
  реализует `DataTemplate` для не-UI контента, хостит `IUIComponent` напрямую либо падает в
  `TextBlock`, и ведёт `TemplateResult`. **Это и есть контейнер элемента.**
- `TemplateResult` ([…/Core/Templates/TemplateResult.cs](Adamantium/Adamantium/Adamantium.UI.Core/Templates/TemplateResult.cs)) — имена, биндинги, триггеры,
  `Destroy()`. Единица, которую генератор реализует/переиспользует.
- `IScrollableContent` ([…/Controls/IScrollableContent.cs](Adamantium/Adamantium/Adamantium.UI.Controls/IScrollableContent.cs)) — шов скролла, написанный
  *прямо под это* («a physical ScrollContentPresenter today, a virtualizing panel later»).
  База виртуализации его реализует.
- `ScrollContentPresenter`/`ScrollViewer` ([…/ScrollContentPresenter.cs](Adamantium/Adamantium/Adamantium.UI.Controls/ScrollContentPresenter.cs),
  [ScrollViewer.cs](Adamantium/Adamantium/Adamantium.UI.Controls/ScrollViewer.cs)) — viewer владеет политикой, контент — механизмом; полосы
  по метрикам в пикселях.
- `DataContext` — **наследуемое** через логического родителя
  ([…/Core/FundamentalUIComponent.cs](Adamantium/Adamantium/Adamantium.UI.Core/FundamentalUIComponent.cs)): `container.DataContext = item` → `{Binding}`
  шаблона резолвятся (движок биндингов уже работает).
- `Panel` ([…/Panels/Panel.cs](Adamantium/Adamantium/Adamantium.UI.Controls/Panels/Panel.cs)) — `[Content]` `Children`, синхронизация visual/logical.
  `StackPanel` ([…/Panels/StackPanel.cs](Adamantium/Adamantium/Adamantium.UI.Controls/Panels/StackPanel.cs)) — образец measure/arrange (его honest-bounds
  поведение сохраняем).
- Динамика коллекций — BCL `ObservableCollection<T>` (INotifyCollectionChanged).

## Архитектура

```
ItemsControl (: Control)
 ├─ ItemsSource : IEnumerable        ItemTemplate : DataTemplate
 ├─ Items : ItemCollection           ItemTemplateSelector / ItemContainerStyle (позже)
 ├─ ItemsPanel : ItemsPanelTemplate  (по умолчанию = StackPanel, виртуализирующий через базу)
 ├─ ItemContainerGenerator           (индексный: Realize/Recycle/ContainerFromIndex)
 └─ Template → Border → ScrollViewer → ItemsPresenter
                                          └─ строит ItemsPanel, связывает его с ItemsControl
                                             ├─ Panel без виртуализации (Canvas/Grid/кастом):
                                             │     презентер генерит ВСЕ контейнеры → panel.Children
                                             └─ VirtualizingPanel (Stack/Wrap):
                                                   сама реализует видимое окно (IScrollableContent)
```

- **`ItemCollection`** — единый наблюдаемый вид над `ItemsSource` **или** над напрямую
  заданными в AUML элементами (`<ItemsControl><Button/>…`). Пробрасывает/поднимает
  `INotifyCollectionChanged`.
- **`ItemContainerGenerator`** — **индексный** API (не курсорный кошмар WPF):
  `Realize(index)`, `Recycle(index)`/`Recycle(container)`, `ContainerFromIndex`,
  `IndexFromContainer`. `Realize`: если элемент уже *является* контейнером/`IUIComponent` —
  используем напрямую; иначе создаём контейнер (по умолчанию `ContentPresenter`), ставим
  `DataContext = item`, `Content = item`, `ContentTemplate = ItemTemplate`. **Recycling ВКЛ по
  умолчанию.** Контракт: **контейнер = чистая проекция айтема**; переживающее переиспользование
  состояние живёт в айтеме/VM.
- **`ItemsPresenter`** — мост в шаблоне контрола: инстанцирует `ItemsPanel`, хостит его, отдаёт
  ему `(ItemsControl + генератор)`.
- **`VirtualizingPanel : Panel`** (база, владеет ВСЕЙ виртуализацией) — реализует
  `IScrollableContent`, держит окно реализованных контейнеров (realize/recycle через генератор),
  считает `Extent`/видимый диапазон/`Offset`, `SetOffset` → пере-measure. Геометрию отдаёт
  наследникам узким seam'ом (`GetVisibleRange(viewport, offset)` + расстановка по индексу).
  **Без пользовательского `IsVirtualizing`** — авто. Действует, только когда панель —
  items-host (есть генератор); как обычный контейнер с явными `Children` ведёт себя как раньше
  (honest-bounds сохраняется). Вырожденный случай (бесконечный размер по оси скролла) — реализует
  всё + **диагностика**, не молча тормозит.
- **`StackPanel : VirtualizingPanel`** — 1D-вариант (текущий StackPanel переезжает на базу).
  Переменная высота допустима (item-based в v1, pixel-accurate позже). Как plain-контейнер —
  без изменений.
- **`WrapPanel : VirtualizingPanel`** — 2D-вариант. Полноценная двухосевая виртуализация при
  **однородной ячейке**: `ItemWidth`/`ItemHeight` заданы явно, иначе размер берём из первого
  реализованного элемента (предполагаем однородность). Тогда позиция элемента i и `Extent` по
  обеим осям — арифметика O(1) → точные скроллы по двум осям (это и есть приемлемая цена).
  Неоднородная обёртка не виртуализируется → realize-all + диагностика.

## Связка ScrollViewer ↔ виртуализирующая панель

Расширяем шов (аналог WPF `CanContentScroll`): в `ScrollContentPresenter` добавляем
`CanContentScroll`; когда `true` и реализованный контент реализует `IScrollableContent` —
презентер **делегирует** `Extent`/`Viewport`/`Offset`/`SetOffset`/метрики этой панели вместо
физического сдвига. Дефолтный шаблон `ItemsControl` ставит `CanContentScroll=true`. `ScrollViewer`
и полосы — без изменений. (Альтернатива с отдельным шаблоном отклонена — дублирует проводку.)

## Улучшения относительно схемы WPF (закладываем сразу)

- **Виртуализация — общая база** (`VirtualizingPanel`), Stack (1D) и Wrap (2D) — лишь геометрия
  на ней. (Направление WinUI ItemsRepeater / Avalonia; в WPF только Stack умел виртуализацию.)
- **Нет ручки «выключить виртуализацию».** Единственный неустранимый невиртуализированный случай
  (нет вьюпорта) панель решает сама (realize-all) + **диагностика**, без молчаливой деградации.
  Осознанно «без виртуализации» = выбор невиртуализирующей раскладки (Canvas/Grid/кастом).
- **2D-виртуализация при однородной ячейке** (`ItemWidth`/`ItemHeight` или вывод из первого
  элемента) → точные скроллы по двум осям; неоднородный wrap → realize-all + диагностика.
- **Индексный генератор** вместо курсорного (`GeneratorPosition`/`StartAt`/`GenerateNext`/батчи).
- **Recycling по умолчанию** + контракт «контейнер = проекция айтема» (убирает баги WPF с
  потерей состояния).
- **Без `ICollectionView` в ядре.** ItemsControl тонкий, слушает `INotifyCollectionChanged`;
  сортировка/фильтр/группировка — в VM (MVVM-first), при желании позже опциональным слоем.
- **`IScrollableContent` вместо `IScrollInfo`** — уже в движке.
- **Extent-модель — под будущий pixel-accurate/anchored скролл** (точный гладкий бар при
  переменной высоте в 1D), хотя v1 — item-based.

## Фазы (каждая проверяется отдельно)

1. **Ядро, без виртуализации.** `ItemCollection`, `ItemsControl` (Items/ItemsSource/
   ItemTemplate, `[Content]` Items), `ItemContainerGenerator` (индексный, recycling),
   `ItemsPresenter`, дефолтный шаблон с обычной раскладкой. Элементы рисуются через контейнеры;
   `{Binding}` элемента резолвится против элемента.
   → *проверка:* тест — N элементов + ItemTemplate ⇒ N контейнеров, контент + `{Binding}` верны.
2. **Динамические коллекции.** Подписка на `INotifyCollectionChanged`; инкрементально
   add/remove/replace/reset.
   → *проверка:* тест — add/remove в `ObservableCollection` обновляет реализованных детей.
3. **Виртуализация: база + 1D StackPanel.** `VirtualizingPanel` (окно, realize/recycle,
   `IScrollableContent`) + `StackPanel` переезжает на базу (item-scrolling), делегация
   `ScrollContentPresenter.CanContentScroll`; дефолтный `ItemsPanel` = StackPanel.
   → *проверка:* тест — 10 000 элементов, фикс. viewport ⇒ реализованы только видимые+буфер;
   скролл сдвигает окно; `Extent ≈ count×itemHeight`; полосы отражают метрики.
4. **2D WrapPanel.** `WrapPanel : VirtualizingPanel` с однородной ячейкой (явная или из первого
   элемента); двухосевое окно + `Extent` по обеим осям арифметикой.
   → *проверка:* тест — 10 000 плиток фикс. размера, фикс. viewport ⇒ реализованы только видимые
   по сетке; скролл по обеим осям корректен; `Extent` по двум осям верен.
5. **Очистка/память.** Очистка ушедших за экран (`TemplateResult.Destroy`), ограниченная память.
   → *проверка:* тест — прокрутка огромного списка держит число реализованных контейнеров
   ограниченным; нет утечки биндингов (проверка через
   [BindingEngine](Adamantium/Adamantium/Adamantium.UI.Core/Data/BindingEngine.cs)).
6. **Тема + AUML-авторинг.** `ItemsControlStyleSet.auml` в FluentDarkTheme (мелкие стили по
   концернам), проводка Border→ScrollViewer→ItemsPresenter; include в `FluentDark.auml`. Убедиться,
   что `<ItemTemplate><DataTemplate>…` и `<ItemsControl.ItemsPanel><ItemsPanelTemplate>…`
   корректно авторятся (`ItemsPanelTemplate` — новый `UiTemplate`, строящий Panel; скорее всего
   переиспользует путь кодогена DataTemplate/ControlTemplate — подтвердить в
   [AumlSourceGenerator.cs](Adamantium/Adamantium/Adamantium.UI.Markup/CodeGeneration/AumlSourceGenerator.cs)).
   → *проверка:* вьюха в Sandbox с `<ItemsControl ItemsSource=… ItemTemplate=…/>` рисуется;
   headless-рендер дизайнера показывает список.

## Ключевые файлы

- **Новые:** `Adamantium.UI.Controls/ItemsControl.cs`, `ItemsPresenter.cs`, `ItemCollection.cs`,
  `Generators/ItemContainerGenerator.cs`, `Panels/VirtualizingPanel.cs`, `Panels/WrapPanel.cs`
  (один тип на файл, file-scoped namespaces, collection expressions `[]`).
- **Новый (Core):** `Adamantium.UI.Core/Templates/ItemsPanelTemplate.cs` (`UiTemplate` → Panel).
- **Правим:** `Panels/StackPanel.cs` (переезд на `VirtualizingPanel`, поведение plain-контейнера
  сохранить), `ScrollContentPresenter.cs` (+`CanContentScroll` с делегацией), `ScrollViewer.cs`
  (проброс `CanContentScroll`). Возможно небольшие хуки на `ContentPresenter` для recycling.
- **Тема:** `Adamantium.UI.Themes/FluentDarkTheme/ItemsControlStyleSet.auml` (+ include в
  `FluentDark.auml`/`FluentLight.auml`).
- **Тесты:** `Tests/Adamantium.UITests/ItemsControlTests.cs` (+ тесты виртуализации 1D и 2D).

## Решения (моя рекомендация — скажи, если не согласен)

- **Виртуализация — общая база `VirtualizingPanel`; Stack (1D) и Wrap (2D) — варианты на ней.**
  Авто, без тумблера. Невиртуализированное = выбор невиртуализирующей раскладки или отсутствие
  вьюпорта (тогда realize-all + диагностика).
- **WrapPanel виртуализируется при однородной ячейке** (явной/выведенной) → точные двухосевые
  скроллы; неоднородный → realize-all + диагностика. Принимаем как приемлемую цену.
- **Индексный генератор + recycling по умолчанию.**
- **Без `ICollectionView`** в ядре; сортировка/фильтр/группировка — VM.
- **Item-based скролл в v1**, pixel-based позже; Extent сразу под апгрейд.
- **Контейнер = ContentPresenter**; выбираемый `ListBoxItem`/`ListBox` — следующий шаг (шов под
  будущую навигацию), не этот план.

## Проверка (end-to-end)

- Headless `Tests/Adamantium.UITests` (сьюта headless): пофазные тесты выше
  (`dotnet test … -p:Platform=x64`). Проверяем число реализованных визуальных детей, контент,
  `{Binding}`, метрики скролла (1D и 2D).
- Вручную: `<ItemsControl>` в `Adamantium.Game.Sandbox`/UI.Sandbox, привязанный к
  `ObservableCollection`, запуск приложения; headless-рендер вьюхи в дизайнере.

## Вне scope (следующее, по твоим словам)

Навигация по вьюхам из вью-моделей — встроенная, «везде и отовсюду», в стиле регионов Prism
(НЕ `NavigationService`/`Frame` из WPF). Машинерия ItemsControl/ContentControl + DataTemplate +
DataContext отсюда — субстрат под неё (регион = хост ContentControl/ItemsControl + сервис
навигации, резолвящий view-by-viewmodel через DataTemplates/DI). Проектируем отдельно после.
