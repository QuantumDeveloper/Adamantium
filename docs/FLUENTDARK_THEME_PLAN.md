# FluentDark тема + парк контролов — план

_Живой документ. Незакоммиченный артефакт. Ссылки на код — `путь:строка` (относительно дерева исходников `Adamantium/`)._
_Решение: довести контролы до продакшн-качества и **постепенно** сформировать полноценную тему **FluentDark** (Fluent Design, тёмная). Первая веха — обычная `Button`._

---

## 0. Цель

1. Починить рендер контролов (корневой баг скруглений), затем привести **Button** к продакшн-виду: корректный скруглённый фон/рамка, полноценное поведение клика, состояния Rest/Hover/Pressed/Disabled/Focus.
2. Завести настоящую палитру токенов **Fluent Dark** — общий фундамент для всех будущих контролов.
3. Расширять парк контролов поверх общей базы (`ButtonBase` → ToggleButton/RepeatButton/CheckBox/RadioButton и далее), темизируя каждый через FluentDark.

Эталон качества — Slate/UIToolkit/Noesis (не Avalonia/MAUI).

---

## 1. Текущее состояние и диагноз (источник истины — код)

Тема **загружается и применяется**: `UIApplication.LoadThemes()` → `ThemeManager.SetTheme(FluentDark)`; при загрузке окна `ApplyCurrentTheme` → `ApplyStyles` → `AttachStyles` → `Button` получает `Template` (`Adamantium.UI.Core/Resources/ThemeManager.cs:25,59`, `Adamantium.UI.Controls/Base/TemplatedUIComponent.cs:45`).

Шаблон Button (`Adamantium.UI.Themes/FluentDarkTheme/ButtonStyleSet.auml`) = `Grid > Border(InnerBorder) > ContentPresenter`. `Border` скругляет **правильно**: `DrawRectangle(Background, innerRect, CornerRadius)` + рамка как `CombinedGeometry` (outer − inner) (`Adamantium.UI.Controls/Decorators/Border.cs:94`).

### 🔴 Корневой баг — двойной рендер
`ContentControl.OnRender` **безусловно** заливает квадрат `DrawRectangle(Background, new Rect(size))` — без CornerRadius и без рамки (`Adamantium.UI.Controls/ContentControl.cs:300`). А `UIComponent.Render` вызывает `OnRender` всегда, независимо от наличия шаблона (`Adamantium.UI.Controls/Base/UIComponent.cs:141`). Поэтому квадратный фон рисуется **под** скруглённым Border шаблона, и его углы торчат за скруглением → «углы не учитывают CornerRadius». Кнопка не «недорисована» — она дорисована лишним квадратом.

### Полный список пробелов

**Рендер**
- `ContentControl.OnRender` рисует квадратный фон даже при наличии шаблона (корневой баг). `Adamantium.UI.Controls/ContentControl.cs:300`
- `Button.CornerRadius` помечен `AffectsMeasure` — должно быть `AffectsRender` (радиус не меняет размер). `Adamantium.UI.Controls/Buttons/Button.cs:22`

**Поведение Button**
- `IsPressed` зарегистрирован, но **нигде не выставляется** — состояния «нажата» нет. `Adamantium.UI.Controls/Buttons/Button.cs:44`
- Click срабатывает на `MouseLeftButtonDown` — мгновенно по нажатию. Нет захвата мыши, нет «отпустил внутри = клик / увёл = отмена», нет `ClickMode`, нет клавиатуры (Space/Enter), нет `IsDefault`/`IsCancel`. `Adamantium.UI.Controls/Buttons/Button.cs:129`
- `Padding` зарегистрирован, но measure/arrange **закомментированы**, и в шаблоне ContentPresenter к Padding не привязан → Padding мёртв. `Adamantium.UI.Controls/Buttons/Button.cs:155`
- Нет `ButtonBase` — Button наследует ContentControl напрямую; будущим кнопкам-сиблингам нужна общая клик-машина.

**Тема (FluentDark Button)**
- ButtonStyleSet — заглушка: фон `DarkGray`, рамка `Yellow`, hover→`Red`, `BorderBrush` = `DeclineBrush` (#F25233). Не Fluent. `Adamantium.UI.Themes/FluentDarkTheme/ButtonStyleSet.auml`
- Один триггер `IsMouseOver→Red`; нет Pressed / Disabled / Focused.
- ContentPresenter не привязан к `Padding`/`Foreground`/`FontSize`; он **безымянный**, хотя `ContentControl.OnApplyTemplate` ищет `PART_ContentPresenter` (`Adamantium.UI.Controls/ContentControl.cs:113`).
- `Brushes.auml` — остаток от прежнего приложения (Approve/Decline/QR/MFA), а не палитра Fluent Dark. `Adamantium.UI.Themes/FluentDarkTheme/Brushes.auml`

---

## 2. План по этапам

### Этап 1 — Починить рендер (фундамент)
- `ContentControl.OnRender`: рисовать Background только когда `Template == null` (удобный путь для бесшаблонного случая). При шаблоне фон/рамку рисует Border шаблона.
- Чтобы не отрегрессить обычный `ContentControl` с фоном (его дефолтный шаблон — голый ContentPresenter), добавить в `ContentControlStyleSet` обрамляющий `Border` с `Background`/`CornerRadius` через TemplateBinding.
- `Button.CornerRadius` → `AffectsRender`; удалить мёртвый закомментированный measure/arrange.
- **Verify:** запуск Sandbox — кнопка скруглена, квадратных углов по краям нет.

### Этап 2 — Палитра Fluent Dark (база для всех контролов)
Завести токены (полупрозрачные overlays в стиле WinUI):
- **Слои:** `SolidBackgroundFillColorBase` (#202020), `CardBackgroundFillColorDefault`, `LayerFillColorDefault`.
- **Control fill:** `Default` / `Secondary` / `Tertiary` / `Disabled`.
- **Control stroke:** `Default` / `Secondary` (+ нижняя «elevation»-грань).
- **Текст:** `TextFillColorPrimary` / `Secondary` / `Tertiary` / `Disabled`.
- **Accent:** `AccentFillColorDefault` / `Secondary` / `Tertiary` / `Disabled` + текст на акценте.
- **Focus:** `FocusStrokeColorOuter` / `Inner`.

### Этап 3 — Button: поведение до прода
- Клик-машина (в `ButtonBase`, см. решение D1): захват мыши на down → `Click` на up-внутри; увод курсора отменяет; `IsPressed` выставляется; `ClickMode` (Press/Release/Hover); клавиатура Space/Enter. Command/CanExecute — перенести как есть (`Adamantium.UI.Controls/Buttons/Button.cs:99-148`).
- Padding/Foreground/FontSize прокинуть в шаблон (TemplateBinding на ContentPresenter; дать ContentPresenter поддержку Padding).

### Этап 4 — Шаблон FluentDark Button + состояния
- Переписать ButtonStyleSet: дефолты (CornerRadius 4, BorderThickness 1, Padding 11,5,11,6, MinHeight 32, фон = ControlFillDefault, рамка = ControlStrokeDefault, текст = TextPrimary, FontSize) + `PART_ContentPresenter` с привязками.
- Триггеры состояний: Hover→ControlFillSecondary; Pressed→ControlFillTertiary + текст Secondary; Disabled→ControlFillDisabled + текст Disabled; Focus→двухслойная рамка (FocusStrokeOuter/Inner).
- Вариант **Accent** — отдельным стилем (селектор по классу).
- Переходы пока мгновенные (PropertyTrigger). Плавные — следующим заходом через готовый animation engine.

### Этап 5 — Расширение парка (позже)
ToggleButton / RepeatButton → CheckBox / RadioButton поверх `ButtonBase` + токенов FluentDark.

---

## 3. Открытые решения (рекомендации по умолчанию)

- **D1. `ButtonBase` выделяем сейчас** — клик-машину пишем один раз и переиспользуем. _Рекомендую: да._
- **D2. Токены полупрозрачные** (правильно по Fluent; у одиночного rounded-rect нет self-overlap, поэтому баг-шов прозрачности (см. `ANALYTIC_AA_PLAN.md`) тут не всплывёт — проверю на запуске). _Запасной вариант:_ заранее скомпозиченные solid-цвета над #202020.
- **D3. Состояния пока на триггерах** (мгновенные); плавные переходы — отдельным заходом.

---

## 4. Roadmap парка контролов (через FluentDark)

1. **Button** (текущая веха) → `ButtonBase`.
2. ToggleButton, RepeatButton.
3. CheckBox, RadioButton.
4. TextBox / поля ввода.
5. ScrollViewer / ScrollBar (RepeatButton + Thumb уже есть — `Adamantium.UI.Controls/Primitives/Thumb.cs`).
6. ListBox / ItemsControl, ComboBox.
7. Slider, ProgressBar.
8. TabControl, Menu, ToolTip.

Каждый контрол: контрол-логика (поведение) + стиль/шаблон в соответствующем `*StyleSet.auml` FluentDark, поверх общих токенов из Этапа 2.
