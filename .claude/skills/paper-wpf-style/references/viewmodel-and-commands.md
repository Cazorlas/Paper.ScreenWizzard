# ViewModels and commands with the shared library's generators

Worked code for `paper-wpf-style`. The rules are in `SKILL.md`; this is what they look like.
`<Lib>` below is the shared WPF library's namespace root, as the project's `CLAUDE.md` names it.

## Generator setup

The ViewModel class must be `partial` and derive from `BindableBase`/`ViewModelBase`, and the project must
reference the generator as an analyzer:

```xml
<ProjectReference Include="..\..\<Lib>.Attributes\<Lib>.Attributes.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

The attribute style is deliberately CommunityToolkit-shaped (`[ObservableProperty]` →
`[BindableBaseProperty]`); the types are the shared library's.

## A ViewModel

```csharp
using <Lib>.WPF.Base;
using <Lib>.WPF.Base.Attributes;

public sealed partial class BrowseViewModel : ViewModelBase
{
    // "_searchText" / "m_searchText" -> generated property "SearchText".
    // The field must not be static, const, or readonly.
    [BindableBaseProperty] private string _searchText = string.Empty;
    [BindableBaseProperty] private bool _isBusy;

    // Computed property: the generator emits NotifyHasChangesChanged(), which raises PropertyChanged
    // and calls RaiseCanExecuteChanged() on each named CommandBase property.
    [RaiseCanExecuteChanged(nameof(ApplyCommand), nameof(ResetCommand))]
    public bool HasChanges => _searchText.Length > 0;

    public ICommand ApplyCommand { get; }
    public ICommand ResetCommand { get; }

    // Generated hooks — implement only what you need.
    partial void OnSearchTextChanged(string oldValue, string newValue) => NotifyHasChangesChanged();
}
```

Generated per field: the property (setter runs `On<Name>Changing(ref value)` → `SetProperty` →
`On<Name>Changed(old, new)`) plus both `partial void` hooks. Generated per
`[RaiseCanExecuteChanged(...)]` property: `private void Notify<Prop>Changed()`.

## Commands

```csharp
using <Lib>.WPF.Commands;

internal sealed class ResetFilterCommand : CommandBase
{
    private readonly BrowseViewModel _viewModel;
    public ResetFilterCommand(BrowseViewModel viewModel) => _viewModel = viewModel;

    public override bool CanExecute(object? parameter) => _viewModel.HasChanges;

    public override void Execute(object? parameter) => _viewModel.SearchText = string.Empty;
}

internal sealed class LoadItemsCommand : AsyncCommandBase
{
    private readonly BrowseViewModel _viewModel;
    private readonly IItemService _itemService;

    public LoadItemsCommand(BrowseViewModel viewModel, IItemService itemService)
    {
        _viewModel = viewModel;
        _itemService = itemService;
    }

    // The base already blocks re-entrancy while running; keep your own guard out of it.
    public override bool CanExecute(object? parameter)
        => base.CanExecute(parameter) && _viewModel.SelectedCategory is not null;

    public override async Task ExecuteAsync(object? parameter)
    {
        _viewModel.IsBusy = true;
        _viewModel.ErrorMessage = null;
        try
        {
            var items = await _itemService.GetAsync(_viewModel.SelectedCategory!);
            _viewModel.Items.Clear();
            foreach (var item in items) _viewModel.Items.Add(item);
        }
        catch (HttpRequestException ex)
        {
            // Recoverable: surface it in the panel and keep the previous list.
            _viewModel.ErrorMessage = ex.Message;
        }
        finally
        {
            // Always restore UI state, including on the failure path.
            _viewModel.IsBusy = false;
        }
    }
}
```

## `[PaperCommand]`

The generator emits the public `ICommand` property and the `new`; the class, its constructor and its
arguments stay written out, because that is the part worth reading.

```csharp
using <Lib>.WPF.Base.Attributes;

// Arguments are named, never inferred. PaperCommandAttribute.Self becomes `this`; everything else is
// a field, property or parameterless method on the class — write it as nameof() so a rename follows.
[PaperCommand(PaperCommandAttribute.Self)] private ClearLogCommand? _clearLog;
[PaperCommand] private OpenLogFolderCommand? _openLogFolder;
[PaperCommand(nameof(_controller))] private ToggleServerCommand? _toggleServer;

public FooViewModel(McpServerController controller)
{
    _controller = controller;

    // Generated. Call it yourself, after every field a command captures has been assigned — only the
    // constructor knows when that is.
    InitializePaperCommands();
}
```

The field must be **nullable and not readonly**: nullable so a non-nullable field does not warn CS8618
(the compiler cannot see the generated assignment), and writable because the generated method assigns it.
`_toggleServer` yields `ToggleServerCommand`; set `Name` to override, or `ExposeConcreteType = true` when a
caller needs `RaiseCanExecuteChanged`.

Errors are compile errors, not silent misbehaviour: `PAPERCMD001`–`PAPERCMD007` cover a missing `partial`, a
field that is not an `ICommand`, a readonly field, a name collision, an argument that names nothing, and an
argument count no constructor accepts.
