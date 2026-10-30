using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;

namespace DoW_Mod_Manager.ViewModels;

/// <summary>
/// MVVM port of the legacy <c>ModMergerForm</c>: pick a playable module, then add, remove, reorder
/// and (de)activate its <c>RequiredMod</c> entries. Changes are saved to a <c>_MERGED</c> copy of the
/// module, which appears as a new mod in the main window.
/// </summary>
public partial class ModMergerViewModel : ViewModelBase
{
    private readonly GameInstall _install;
    private readonly IModMergerService _merger;
    private readonly IDialogService _dialogs;
    private readonly Action _onModulesChanged;

    private IReadOnlyList<string> _allModules = [];
    private IReadOnlyList<string> _baseModules = [];

    public ObservableCollection<MergeableModule> Modules { get; } = [];
    public ObservableCollection<MergeEntry> Entries { get; } = [];
    public ObservableCollection<string> AvailableMods { get; } = [];

    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteMergeCommand))]
    [ObservableProperty] private MergeableModule? _selectedModule;

    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(ActivateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [ObservableProperty] private MergeEntry? _selectedEntry;

    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [ObservableProperty] private string? _selectedAvailableMod;

    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [ObservableProperty] private bool _isDirty;

    [ObservableProperty] private string _status = "";

    public ModMergerViewModel(
        GameInstall install,
        IModMergerService merger,
        IDialogService dialogs,
        Action onModulesChanged)
    {
        _install = install;
        _merger = merger;
        _dialogs = dialogs;
        _onModulesChanged = onModulesChanged;

        Refresh();
    }

    /// <summary>Reloads the module lists from disk, keeping the current selection when possible.</summary>
    private void Refresh()
    {
        string? selectedName = SelectedModule?.Name;
        try
        {
            _allModules = _merger.GetAllModuleNames(_install);

            Modules.Clear();
            foreach (var module in _merger.GetMergeableModules(_install))
                Modules.Add(module);

            SelectedModule = Modules.FirstOrDefault(m => m.Name == selectedName) ?? Modules.FirstOrDefault();
            if (Modules.Count == 0)
                Status = "No playable mod found in this installation.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to read modules: {ex.Message}";
        }
    }

    partial void OnSelectedModuleChanged(MergeableModule? value)
    {
        _baseModules = [];
        SetEntries([]);
        IsDirty = false;
        if (value is null)
            return;

        try
        {
            MergeDefinition def = _merger.Load(value.EffectiveFilePath, _allModules);
            _baseModules = def.BaseModules;
            SetEntries(def.Entries);
            Status = value.HasMerge
                ? $"Editing the merged copy of {value.Name}."
                : $"Editing {value.Name}. Saving creates {value.Name}{IModMergerService.MergePostfix}.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to read {value.Name}: {ex.Message}";
        }
    }

    /// <summary>Replaces the entry list (inactive/missing sorted last) and refreshes the available pool.</summary>
    private void SetEntries(IEnumerable<MergeEntry> entries, string? reselect = null)
    {
        Entries.Clear();
        foreach (var entry in ModMergerService.SortInactiveToBottom(entries))
            Entries.Add(entry);

        SelectedEntry = reselect is null ? null : Entries.FirstOrDefault(e => e.Name == reselect);

        AvailableMods.Clear();
        var used = new HashSet<string>(Entries.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
        foreach (string mod in _allModules)
        {
            bool isSelf = SelectedModule is not null &&
                (mod.Equals(SelectedModule.Name, StringComparison.OrdinalIgnoreCase) ||
                 mod.Equals(SelectedModule.Name + IModMergerService.MergePostfix, StringComparison.OrdinalIgnoreCase));
            if (!isSelf && !used.Contains(mod))
                AvailableMods.Add(mod);
        }

        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        DeactivateCommand.NotifyCanExecuteChanged();
    }

    private void Edit(Func<List<MergeEntry>, string?> change)
    {
        var list = Entries.ToList();
        string? reselect = change(list);
        SetEntries(list, reselect);
        IsDirty = true;
    }

    private int SelectedIndex => SelectedEntry is null ? -1 : Entries.IndexOf(SelectedEntry);
    private int ActiveCount => Entries.Count(e => e.State == MergeModState.Active);

    private bool CanAdd => SelectedAvailableMod is not null;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        string mod = SelectedAvailableMod!;
        Edit(list =>
        {
            list.Add(new MergeEntry(mod, MergeModState.Active));
            return mod;
        });
        SelectedAvailableMod = AvailableMods.FirstOrDefault();
    }

    private bool HasSelection => SelectedEntry is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        int index = SelectedIndex;
        Edit(list =>
        {
            list.RemoveAt(index);
            return list.Count > 0 ? list[Math.Min(index, list.Count - 1)].Name : null;
        });
    }

    private bool CanActivate => SelectedEntry?.State == MergeModState.Inactive;

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private void Activate() => SetSelectedState(MergeModState.Active);

    // At least one mod must stay active (legacy guard in ButtonDeactivate_Click).
    private bool CanDeactivate => SelectedEntry?.State == MergeModState.Active && ActiveCount > 1;

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private void Deactivate() => SetSelectedState(MergeModState.Inactive);

    private void SetSelectedState(MergeModState state)
    {
        int index = SelectedIndex;
        Edit(list =>
        {
            list[index] = list[index] with { State = state };
            return list[index].Name;
        });
    }

    private bool CanMoveUp => SelectedIndex > 0;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Swap(SelectedIndex, SelectedIndex - 1);

    private bool CanMoveDown => SelectedIndex >= 0 && SelectedIndex < Entries.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Swap(SelectedIndex, SelectedIndex + 1);

    private void Swap(int from, int to)
    {
        Edit(list =>
        {
            (list[from], list[to]) = (list[to], list[from]);
            return list[to].Name;
        });
    }

    private bool CanSave => SelectedModule is not null && IsDirty;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            string path = _merger.Save(SelectedModule!, new MergeDefinition
            {
                Entries = Entries.ToList(),
                BaseModules = _baseModules,
            });
            IsDirty = false;
            Refresh();
            _onModulesChanged();
            await _dialogs.ShowMessageAsync(
                $"Merged module saved to:\n{path}\n\nYou can now select the merged mod in the Mod Manager to play it.",
                "Saving successful!");
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync(ex.Message, "Saving failed");
        }
    }

    private bool CanDeleteMerge => SelectedModule?.HasMerge == true;

    [RelayCommand(CanExecute = nameof(CanDeleteMerge))]
    private async Task DeleteMergeAsync()
    {
        var module = SelectedModule!;
        if (!await _dialogs.ConfirmAsync($"Delete the merged copy of {module.Name} and revert all changes?", "Delete merge"))
            return;

        try
        {
            _merger.DeleteMerge(module);
            Refresh();
            _onModulesChanged();
            Status = $"Merged copy of {module.Name} deleted.";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync(ex.Message, "Delete failed");
        }
    }

    [RelayCommand]
    private void Close() => RequestClose();
}
