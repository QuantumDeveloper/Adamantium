# Навигационный сервис на уровне ViewModel (Prism, но улучшенный, на адаптерах) + диалоги

## Контекст

В Adamantium есть весь MVVM/DI-субстрат (DI-контейнер с авто-транзиент резолвом, `IEventAggregator`,
`AdamantiumViewModel` + генератор `[Bindable]`/`[Command]`, `ContentControl`/`ContentPresenter` со встроенной анимацией
смены контента `ContentTransition`, `Selector`/`TabControl`, `View : ContentControl`, окна `WindowBase`/`Window` +
`UIApplication.AddWindow`/`Show`, оверлей-слой `Popup`/`PopupLayer` + `SlidePanel`), но **навигации и диалогов нет**. Сейчас
Sandbox имитирует навигацию: `TabControl` привязан к `GalleryViewModel.Tabs`/`SelectedTab`, вью подбирает sandbox-only
`TabViewSelector` (`FooViewModel`→`FooView` по имени). Роадмап (`docs/ITEMSCONTROL_VIRTUALIZATION_PLAN.md:216`) описывает
нужное: «навигация из VM в стиле Prism-регионов, регион = хост-контрол + сервис, резолвящий view-by-viewmodel через DI».

**Цель:** сервис навигации на уровне ViewModel, управляемый **полностью из VM без касания Views**, интегрированный в UI
**через интерфейсы** (каждый кусок заменяем через DI), нацеленный на **любой хост-контрол** (ContentControl И мульти-селектор
типа TabControl, и будущий DockingControl), с **понятной удобной расширяемостью**. Как Prism-регионы, но улучшенные:
типизированная навигация (по типу VM, не по строкам), async-lifecycle и гарды, инжект `INavigationService`/`IRegion` (без
сервис-локатора), чистая модель **региональных адаптеров**. Плюс **оконная навигация из VM**, менеджер навигации как
**постоянный член `IUIApplication`**, и (фаза 2) **диалог-сервис со смешанным хостингом** (оверлей + модальное окно).

Подтверждённые решения (пользователь): оба способа привязки региона (VM-owned + named); один проект
**`Adamantium.Navigation`** под контракты+движок; полный v1 навигации (адаптеры ContentControl + Selector/TabControl,
ViewLocator, журнал, async-гард, типизированные параметры, lifecycle, оконная навигация, демо со **вторым окном**); менеджер
живёт в `IUIApplication`; **диалоги — фаза 2, смешанный хостинг через `IDialogHost`** (не отложены); docking-адаптер — на
будущее.

## Архитектура (слои)

**Движок навигации/диалогов работает с ViewModel'ями как с `object` + `Type` + DI-резолвером — не касается ни одного
UI-типа.** Реализация вью (VM→View), создание окон и хосты диалогов — задачи UI-слоя (адаптеры, window-backend, dialog-хосты).

- **`Adamantium.Navigation`** (новый проект, зависит **только от `Adamantium.Core`**) — всё, что видит ViewModel: контракты,
  POCO, UI-free движок навигации И диалогов, UI-free интерфейсы `IWindowNavigationBackend`/`IDialogHost`.
- **`Adamantium.UI.Core`** — добавляет ссылку на `Adamantium.Navigation`, чтобы `IUIApplication` выставил менеджер навигации
  постоянным членом (цикла нет: `Adamantium.Navigation` не ссылается на UI.Core).
- **`Adamantium.UI.Controls`** — view-резолв, региональные адаптеры, привязка региона к контролам, `OverlayDialogHost`.
- **`Adamantium.UI`** — `WindowNavigationBackend` + `WindowDialogHost` + проводка DI + инициализация менеджера на `UIApplication`.

ViewModel ссылается только на `Adamantium.Navigation` и навигирует / открывает окна / показывает диалоги с **нулевой
UI-зависимостью**.

## Что живёт в `Adamantium.Navigation`

Один проект (один тип на файл, file-scoped namespace `Adamantium.Navigation`):

**1. VM-facing контракты:**
- `INavigationService` — фасад VM: `DefaultRegion`, `Regions`, `NavigateToAsync`/`GoBack`/`GoForward`, и **оконная
  навигация** `Task<NavigationResult> OpenWindowAsync(Type contentVmType, NavigationParameters=null, string windowShell=null,
  CancellationToken=default)` / `OpenWindowAsync<TContentVm>(windowShell=null)` / `CloseWindowAsync(object contentVm)`.
  `windowShell` — ключ кастомного оконного шелла (null = дефолт).
- `IRegion` (`INotifyPropertyChanged`) — control-агностичная цель: `Name`, `CurrentViewModel`, `ActiveViewModels`
  (мульти-вью), `Journal`, `CanGoBack/Forward`; `NavigateToAsync<TVm>(params)`/`(Type,params)`,
  `Add/Remove/Activate/Deactivate(object vm)`, `GoBackAsync/GoForwardAsync`; события `Navigated`/`ActiveViewsChanged`.
- `IRegionManager` — `this[string]`, `GetOrCreateRegion`, `CreateRegion(name=null)`, `TryGetRegion`, `RegisterRegion`, `Regions`.
- `INavigationAware` — `OnNavigatedTo/From(ctx)`, `bool IsNavigationTarget(ctx)`. `IConfirmNavigation` —
  `Task<bool> CanNavigateAwayAsync(ctx, ct)`. `INavigationJournal` — стеки + `RecordNavigation`/`Back`/`Forward`/`Clear`.
- `IWindowNavigationBackend` — UI-free мост к окнам (реализация в `Adamantium.UI`): `Task<object> OpenWindowAsync(object
  contentVm, NavigationParameters, string windowShell, CancellationToken)`, `void CloseWindow(object contentVm)`.
  `IWindowAware` (опц.) — content-VM задаёт заголовок/размер/позицию **и `string WindowShellKey`** (в каком кастомном
  оконном шелле хостить контент) без касания UI.
- **Диалоги (фаза 2):** `IDialogService` — VM-first, host-агностичен: `Task<IDialogResult> ShowDialogAsync(Type dialogVmType,
  NavigationParameters parameters=null, DialogHostKind host=DialogHostKind.Default, CancellationToken=default)` +
  `ShowDialogAsync<TDialogVm>(...)`. `IDialogAware` — `OnDialogOpened(NavigationParameters)`, `bool CanCloseDialog()`,
  `event Action<IDialogResult> RequestClose` (VM закрывает себя с результатом). `IDialogResult` — `DialogButtonResult Result`
  (`Ok/Cancel/Yes/No/None`) + `NavigationParameters Parameters` (возвраты). `IDialogHost` — UI-free абстракция хоста:
  `DialogHostKind Kind`, `Task<IDialogResult> ShowAsync(object dialogVm, NavigationParameters, CancellationToken)`.
  `IDialogHostRegistry` (как `RegionAdapterMappings`): `Register(kind, host)`, `Get(kind)`, `Default`. Enum `DialogHostKind`
  (`Default|Overlay|Window`).

**2. POCO:** `NavigationContext`, `NavigationMode` (`New|Back|Forward|Refresh`), `NavigationParameters` (словарь +
`GetValue<T>`/`TryGetValue<T>` + fluent `Add` + ctor из `"id=5"`; **используется и навигацией, и диалогами**),
`NavigationResult` (`Ok`/`Vetoed`/`Failed`), `NavigationJournalEntry`, `RegionNavigationEventArgs`.

**3. Движок по умолчанию (конкретный, UI-free, заменяем через DI):** `NavigationService` (регионы; оконную навигацию — в
`IWindowNavigationBackend`), `Region` (lifecycle + журнал + `ActiveViewModels`, резолвит VM через `IDependencyResolver`),
`RegionManager`, `NavigationJournal`, и (фаза 2) `DialogService` (резолвит dialog-VM, берёт хост из `IDialogHostRegistry`
по kind, делегирует, ждёт `RequestClose` через TaskCompletionSource). Опционально `NavigationEvent :
BasicAggregatorEvent<NavigationContext>`.

## Менеджер навигации как постоянный член `IUIApplication`

`IUIApplication` (Adamantium.UI.Core) получает `INavigationService Navigation { get; }`. `UIApplication` в `Initialize`
резолвит менеджер (синглтон), держит и выставляет через `Navigation`; тот же инстанс в DI — инжект в VM, `app.Navigation`,
`UIAppContext.Current.Application.Navigation` дают одно. Менеджер живёт всё время приложения. (Симметрично при желании
выставим `IDialogService Dialogs { get; }` там же в фазе 2.)

## UI-интеграция: региональные адаптеры (в `Adamantium.UI.Controls/Navigation/`)

- `IViewLocator` (возвращает `IUIComponent`): `ResolveView(object vm)` (создаёт вью + ставит `DataContext=vm`),
  `ResolveViewType(Type)`, `CreateViewInstance(Type)`, `Register(vmType, viewType)`/`Register<TVm,TView>()`,
  `RegisterFactory`, `RegisterKey`. `ViewLocator` — фабрика → явный маппинг → **соглашение** (`FooViewModel`→`FooView`,
  обобщение `TabViewSelector`, кэш) → фолбэк; инстансы **через DI**.
- `IRegionAdapter` — `void Attach(IRegion, IUIComponent host)`. `RegionAdapterMappings` мапит тип контрола → адаптер (вверх
  по иерархии). Встроенные: `ContentControlRegionAdapter` (`CurrentViewModel`→`Content`, переиспользует
  `ContentTransition`), `SelectorRegionAdapter` (покрывает `TabControl`: `ActiveViewModels`→`ItemsSource`,
  `ContentTemplateSelector=ViewLocatorTemplateSelector`, синхрон `CurrentViewModel`↔`SelectedItem` — **это и есть «TabControl
  на менеджере навигации»**), `ItemsControlRegionAdapter`. `ViewLocatorTemplateSelector : DataTemplateSelector` — замена
  `TabViewSelector` (потом удаляется).
- Привязка региона к контролу — **оба способа**: VM-owned attached-свойство `Region.Source`
  (`<TabControl nav:Region.Source="{Binding TabsRegion}"/>`) и named `RegionManager.RegionName`
  (`<TabControl nav:RegionManager.RegionName="workspace"/>`). Оба резолвят адаптер по типу контрола и зовут `Attach`.

## Оконная навигация + кастомный оконный шелл (v1)

Отделяем **оконный шелл** (окно со своими стилями/chrome/улучшениями) от **контента**, который в него грузится.

- `IWindowShellRegistry` (UI-сторона, `Adamantium.UI.Controls`): `Register(string key, Func<IWindow>)` /
  `Register<TWindow>(key)` где `TWindow : Window`, `IWindow Create(string key)`, `string DefaultKey`. Фреймворк
  регистрирует `"default"` = темизированный `Window`; приложение регистрирует **свои `Window`-сабклассы** (свои стили,
  TitleBar, chrome) под ключами и/или переопределяет `"default"`.
- `WindowNavigationBackend` (`Adamantium.UI`) реализует `IWindowNavigationBackend`: инжектит `IUIApplication` (уже
  `RegisterInstance<IUIApplication>`), `IViewLocator`, `IWindowShellRegistry`, `IUIContext`. `OpenWindowAsync(contentVm,
  params, windowShell)`: ключ = `windowShell ?? (contentVm as IWindowAware)?.WindowShellKey ?? DefaultKey` → `shell =
  registry.Create(key)` (кастомное окно) → `shell.Content = viewLocator.ResolveView(contentVm)` (грузим контент в шелл;
  chrome живёт в шаблоне/TitleBar окна, `Content` = клиентская область) → `application.AddWindow(shell)` +
  `shell.AttachContextAndInitialize(UIContext)` + `shell.Show()` (паттерн вторичных окон из `DesignerSession`); запоминает
  `contentVm → shell`. Регистрируется в DI. Тот же реестр переиспользуется и для стартового окна.
- `NavigationService.OpenWindowAsync` резолвит content-VM (DI) и делегирует — **VM не создаёт Window явно, только называет
  шелл ключом** (или через `IWindowAware`). Расширяемость: свой шелл = `windowShells.Register<MyToolWindow>("tool")`.

## Диалог-сервис (фаза 2, смешанный хостинг)

`IDialogService` host-агностичен и VM-first (тот же паттерн): резолвит dialog-VM (DI), вью — через тот же `IViewLocator`,
ждёт результат через `IDialogAware.RequestClose`/кнопки диалога (TaskCompletionSource). Хостинг — `DialogHostKind`
(Default→Overlay). Два хоста реализуют `IDialogHost` и регистрируются в `IDialogHostRegistry`:
- `OverlayDialogHost` (Kind=Overlay, `Adamantium.UI.Controls`) — scrim + центрированный диалог на `PopupLayer` активного
  окна (паттерн `SlidePanel`/`Popup`), в границах окна; вью через `IViewLocator`.
- `WindowDialogHost` (Kind=Window, `Adamantium.UI`) — отдельное **модальное** окно поверх window-backend'а: блокировка ввода
  owner-окна + await результата (**модальная обвязка net-new** — сейчас нет `ShowDialog`/owner/DialogResult).
Расширяемо: новый хост = реализовать `IDialogHost` + `registry.Register(kind, host)`.

## Последовательность навигации (в `Region.NavigateToAsync`)

`NavigationContext` (mode `New`, source, целевой тип, params, ct) → **гард ухода** (`IConfirmNavigation`; отменён ⇒ `Vetoed`)
→ **переиспользование** (`IsNavigationTarget` ⇒ `Refresh`, иначе `resolver.Resolve`) → `OnNavigatedFrom`+`OnNavigatedTo` →
`Journal.RecordNavigation` → выставляем `CurrentViewModel`/`ActiveViewModels` (нотификация ⇒ адаптер меняет вью + транзишен)
→ `Navigated` → `Ok`. `GoBack/GoForward` — гард, поп/пуш журнала, без `RecordNavigation`. Throw ⇒ `Failed`.

## Проводка DI (batteries included, заменяемо)

Guarded-дефолты в `UIApplication.RegisterServices` (`UIApplication.cs:381`; `GameApplication` зовёт `base`):
`RegisterInstance<IDependencyResolver>(Container)`, `RegisterSingleton` для `IViewLocator`/`RegionAdapterMappings`/
`IWindowShellRegistry` (+ регистрация дефолтного шелла `"default"` → `Window`)/`IRegionManager`/`IWindowNavigationBackend`/
`INavigationService`, а в фазе 2 — `IDialogHostRegistry`/`IDialogService` +
регистрация `OverlayDialogHost`/`WindowDialogHost` в реестр. Каждый под `if (!IsRegistered<T>())` (регистрация своего до
`base` выигрывает). После — `UIApplication` резолвит `Navigation`. VM получают сервисы инжектом (один ctor).

## Расширяемость

1. Вью↔VM: соглашение / `viewLocator.Register<TVm,TView>()` / `RegisterFactory`.
2. Новый хост-контрол: `class DockingRegionAdapter : IRegionAdapter` + `regionAdapterMappings.Register<DockingControl,…>()`.
3. Кастомное окно: свой `Window`-сабкласс (стили/chrome) + `windowShells.Register<MyWindow>("key")` (или переопределить `"default"`).
4. Новый хост диалога / замена любого сервиса: реализовать интерфейс + зарегистрировать в override `RegisterServices` до `base`.
5. Nav/dialog-aware VM: `INavigationAware`/`IConfirmNavigation`/`IWindowAware`/`IDialogAware` (или базовые классы в MVVM).

## Демо в Sandbox (управляется из VM; ДВА окна; диалоги в фазе 2)

- Поглотить `TabViewSelector` → в `GalleryView.auml` заменить на `<controls:ViewLocatorTemplateSelector/>`; удалить
  `TabViewSelector.cs`.
- **Таб Navigation** (ContentControl-регион): `NavigationDemoViewModel(INavigationService nav)` владеет
  `nav.Regions.CreateRegion()`, async `[Command]` `GoHome/OpenDetails(id=42)/GoSettings`/`Back/Forward`; page-VM
  `Home/Details/Settings` (`INavigationAware`), `Details`—`IConfirmNavigation` (вето). Вью:
  `<ContentControl nav:Region.Source="{Binding Region}" ContentTransition="SlideLeft"/>`.
- **Второе окно через команду в шапке главного окна** (в кастомном шелле): `MainWindow.auml` (title-bar) → `[Command]` на
  `MainViewModel(INavigationService nav)` → `nav.OpenWindowAsync<WorkspaceShellViewModel>(windowShell: "workspace")`, где
  `"workspace"` зарегистрирован на кастомный `WorkspaceWindow` (свой стиль/chrome). Окно создаёт фреймворк, контент грузится
  в шелл, VM Window не касается.
- **Второе окно — TabControl сугубо на менеджере**: `WorkspaceShellViewModel(INavigationService nav)` создаёт
  `nav.Regions.GetOrCreateRegion("workspace")`; `WorkspaceShellView.auml` = `<TabControl nav:RegionManager.RegionName=
  "workspace"/>` + кнопки `OpenDoc(id)/OpenChart/CloseTab` → `nav.Regions["workspace"].NavigateToAsync<...>()`. Табы =
  активные вью региона (через `SelectorRegionAdapter`). Page-VM `Doc/Chart` + вью.
- **Фаза 2 — демо диалогов**: из page-VM `dialogService.ShowDialogAsync<ConfirmDialogViewModel>(...)` оверлеем и
  `ShowDialogAsync<..>(host: DialogHostKind.Window)` окном; await результата, реакция. `ConfirmDialogViewModel :
  IDialogAware` с Ok/Cancel-командами через `RequestClose`.
- `GalleryViewModel(IDependencyResolver resolver)` добавляет `resolver.Resolve<NavigationDemoViewModel>()` в `Tabs`.

## Пофайловый список изменений

- **Новый `Adamantium.Navigation/`** (ref `Adamantium.Core`; в `Adamantium.sln`): контракты навигации (+`IWindowNavigationBackend`,
  `IWindowAware`), POCO, движок; **фаза 2** — контракты диалогов (`IDialogService`/`IDialogAware`/`IDialogResult`/`IDialogHost`/
  `IDialogHostRegistry`/`DialogHostKind`) + `DialogService` (один тип на файл).
- **`Adamantium.UI.Core`**: `IUIApplication.cs` (+`Navigation`) + ref csproj.
- **`Adamantium.UI.Controls/Navigation/`** (ref `Adamantium.Navigation`): `IViewLocator`/`ViewLocator`/
  `ViewLocatorTemplateSelector`, `IRegionAdapter`/`RegionAdapterMappings`/3 адаптера, attached `Region`/`RegionManager`,
  `IWindowShellRegistry`/`WindowShellRegistry`, опц. `RegionControl`; **фаза 2** — `OverlayDialogHost`.
- **`Adamantium.UI`**: `WindowNavigationBackend.cs`; `UIApplication.cs` (`RegisterServices` + init `Navigation`); **фаза 2** —
  `WindowDialogHost.cs` + модальная обвязка; ref csproj.
- **`Adamantium.MVVM`** (опц.): `NavigationAwareViewModel`/`DialogViewModel` базы.
- **`Adamantium.Game.Sandbox`**: удалить `TabViewSelector.cs`; правка `GalleryView.auml`/`GalleryViewModel.cs`/
  `MainWindow.auml`/`MainViewModel.cs`; +`NavigationDemoViewModel` + `Home/Details/Settings`, +`WorkspaceShellViewModel` +
  `Doc/Chart` + вью, + кастомный `WorkspaceWindow` (оконный шелл, свой стиль); **фаза 2** — +`ConfirmDialogViewModel` + вью.

Конвенции: один тип на файл, file-scoped namespaces, `[]` collection expressions, без code-behind, мелкие focused-стили.

## Фазы выполнения

**v1 — навигация:**
1. Движок + контракты в `Adamantium.Navigation` (собирается только против `Adamantium.Core`).
2. UI-слой: `ViewLocator`/`ViewLocatorTemplateSelector`, `RegionAdapterMappings` + адаптеры, attached-свойства.
3. Оконная навигация + приложение: `WindowNavigationBackend`, `IUIApplication.Navigation`, проводка DI.
4. Демо в Sandbox: таб Navigation + открытие второго окна + `WorkspaceShell` с TabControl-регионом.

**Фаза 2 — диалоги:**
5. Диалог-сервис: контракты + `DialogService` (UI-free) в `Adamantium.Navigation`; `OverlayDialogHost` (PopupLayer) и
   `WindowDialogHost` (модальное окно, net-new обвязка) + `IDialogHostRegistry`; регистрация; демо (оверлей + окно из page-VM).

**Отложено (позже):** `DockingRegionAdapter` (когда появится docking-контрол).

## Проверка (end-to-end)

- Сборка x64: `dotnet build Adamantium.sln -p:Platform=x64`. `Adamantium.Navigation` компилится только против `Adamantium.Core`;
  `IUIApplication.Navigation` доступен; Sandbox линкуется.
- Таб **Navigation**: `HomePageView` в регионе со своим DataContext; **Details** — `SlideLeft` + `id=42`; **Back/Forward** и
  доступность; вето `IConfirmNavigation` блокирует смену.
- **Второе окно + кастомный шелл**: команда в шапке открывает второе окно в кастомном `WorkspaceWindow`-шелле (свои
  стили/chrome) с загруженным контентом; VM окно не создавала.
- **TabControl на менеджере**: во втором окне команды открывают/активируют/закрывают табы через `Navigation.Regions
  ["workspace"]`; табы = активные вью, `SelectedItem`↔`CurrentViewModel`.
- **Фаза 2**: диалог показывается оверлеем и отдельным модальным окном; `await` возвращает `IDialogResult`; Ok/Cancel меняют
  результат.
- Grep новых `.auml`: **нет кода навигации, одна привязка региона на хост**; VM не создают Window/TabItem/диалог-окно вручную.
- (Если UITests компилятся) headless-тесты движка: навигация/вето/журнал против фейкового `IDependencyResolver`, оконная
  навигация против фейкового `IWindowNavigationBackend`, диалоги против фейкового `IDialogHost`.
