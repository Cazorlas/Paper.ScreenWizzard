# XAML on the shared Paper WPF library

Reference for `paper-wpf-style`. Check the shared library's `WPF/` folder before writing XAML; do not hand-roll a style,
converter, icon, or dialog that already exists. `<Lib>` is the library's namespace root and `<LibAssembly>` its
assembly name, as the project's `CLAUDE.md` names them.

| Need | Use |
| --- | --- |
| Themed window / user control | `<Lib>.WPF.Base.PaperWindow`, `PaperUserControl` (set `IsThemed` + `Paper.Background` in their ctor) |
| Control styles | `WPF/Themes/<Lib>.Defaults.xaml` (Button, TextBox, ComboBox, DataGrid, Expander, Scroll, Tooltip, ProgressBar, Icon, Shadows, converters…) |
| Colors | `<Lib>.LightColors.xaml` / `DarkColors.xaml` → `Paper.Background`, `Paper.Border`, `Paper.Primary.Text`, `Paper.Primary.SecondaryText`, `Paper.HoverBrush`, … |
| Icons | `WPF/Themes/<Lib>.Icon.xaml` `Geometry` keys → `<Path Data="{StaticResource Search}" />`; `IconKeyToGeometryConverter` / `IconToImageSourceConverter` when bound |
| Converters | `WPF/Converters/` (~30, already merged by `Defaults.xaml`) |
| Controls | `WPF/Controls/` — `SearchableComboBox`, `NumericUpDownWithSuffix`, `Pagination`, `FlexPanel`, `RunningLine` |
| Dialogs / progress | `WPF/Components/` — `PaperInputDialog`, `PaperSelectionDialog`, `WarningBar`, `AdvancedProgressBar`, `ProgressBarTypeA/B`, `IProgressBar` |

## Standard UserControl header

```xml
<UserControl
    x:Class="…Views.Controls.FooPanelControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:…ViewModels"
    xmlns:wpfUtil="clr-namespace:<Lib>.WPF.Utilities;assembly=<LibAssembly>"
    d:DataContext="{d:DesignInstance vm:FooPanelViewModel}"
    wpfUtil:ThemeUtility.IsThemed="True"
    mc:Ignorable="d">

    <!--  DataContext = FooPanelViewModel, set by the parent via DataContext="{Binding FooPanel}"  -->

    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/<LibAssembly>;component/WPF/Themes/<Lib>.LightColors.xaml" />
                <ResourceDictionary Source="pack://application:,,,/<LibAssembly>;component/WPF/Themes/<Lib>.Defaults.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </UserControl.Resources>
    …
</UserControl>
```

The assembly name in the pack URIs (`<LibAssembly>`, version suffix included) must match the referenced build; a mismatch fails
at runtime, not at compile time.
