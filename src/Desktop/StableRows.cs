using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AmuleModern.Amule;

namespace AmuleModern.Desktop;

// DataGrid leaks native render tiles when a row object is replaced. These rows stay
// in the collection and only raise PropertyChanged for values that actually changed.
internal static class StableRows
{
    public static void Sync<TModel, TRow>(ObservableCollection<TRow> rows, IReadOnlyList<TModel> incoming, Func<TRow, string> rowKey, Func<TModel, string> modelKey, Func<TModel, TRow> create, Action<TRow, TModel> update)
        where TRow : class
    {
        var valid = incoming.Select(modelKey).ToHashSet(StringComparer.Ordinal);
        for (int i = rows.Count - 1; i >= 0; i--)
            if (!valid.Contains(rowKey(rows[i]))) rows.RemoveAt(i);
        foreach (var model in incoming)
        {
            string key = modelKey(model);
            int index = -1;
            for (int i = 0; i < rows.Count; i++)
                if (string.Equals(rowKey(rows[i]), key, StringComparison.Ordinal)) { index = i; break; }
            if (index < 0) rows.Add(create(model));
            else update(rows[index], model);
        }
    }
}

internal abstract class StableRow<T> : INotifyPropertyChanged where T : class
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public T Model { get; private set; }
    protected StableRow(T model) => Model = model;

    public void Apply(T next)
    {
        if (EqualityComparer<T>.Default.Equals(Model, next)) return;
        var previous = Model;
        Model = next;
        OnUpdated(previous, next);
    }

    protected abstract void OnUpdated(T previous, T next);

    protected void Changed<TValue>(TValue previous, TValue next, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<TValue>.Default.Equals(previous, next) && name != null)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

internal sealed class DownloadRow : StableRow<DownloadItem>
{
    public DownloadRow(DownloadItem model) : base(model) { }
    public string Hash => Model.Hash;
    public string Name => Model.Name;
    public ulong Size => Model.Size;
    public string SizeText => Model.SizeText;
    public double Progress => Model.Progress;
    public string ProgressText => Model.ProgressText;
    public string StateText => Model.StateText;
    public ulong Speed => Model.Speed;
    public string SpeedText => Model.SpeedText;
    public double? EtaSeconds => Model.EtaSeconds;
    public string EtaText => Model.EtaText;
    public ulong Sources => Model.Sources;
    public byte State => Model.State;
    public bool CanCancel => Model.CanCancel;
    public bool IsComplete => Model.IsComplete;
    public ulong EcId => Model.EcId;
    public DownloadDetail Detail => Model.Detail;

    protected override void OnUpdated(DownloadItem previous, DownloadItem next)
    {
        Changed(previous.Name, next.Name, nameof(Name));
        Changed(previous.Size, next.Size, nameof(Size));
        Changed(previous.SizeText, next.SizeText, nameof(SizeText));
        Changed(previous.Progress, next.Progress, nameof(Progress));
        Changed(previous.ProgressText, next.ProgressText, nameof(ProgressText));
        Changed(previous.StateText, next.StateText, nameof(StateText));
        Changed(previous.Speed, next.Speed, nameof(Speed));
        Changed(previous.SpeedText, next.SpeedText, nameof(SpeedText));
        Changed(previous.EtaSeconds, next.EtaSeconds, nameof(EtaSeconds));
        Changed(previous.EtaText, next.EtaText, nameof(EtaText));
        Changed(previous.Sources, next.Sources, nameof(Sources));
        Changed(previous.State, next.State, nameof(State));
        Changed(previous.IsComplete, next.IsComplete, nameof(IsComplete));
        Changed(previous.EcId, next.EcId, nameof(EcId));
    }
}

internal sealed class SearchRow : StableRow<SearchResult>
{
    public SearchRow(SearchResult model) : base(model) { }
    public string Hash => Model.Hash;
    public string Name => Model.Name;
    public ulong Size => Model.Size;
    public string SizeText => Model.SizeText;
    public ulong Sources => Model.Sources;
    public ulong CompleteSources => Model.CompleteSources;

    protected override void OnUpdated(SearchResult previous, SearchResult next)
    {
        Changed(previous.Name, next.Name, nameof(Name));
        Changed(previous.Size, next.Size, nameof(Size));
        Changed(previous.SizeText, next.SizeText, nameof(SizeText));
        Changed(previous.Sources, next.Sources, nameof(Sources));
        Changed(previous.CompleteSources, next.CompleteSources, nameof(CompleteSources));
    }
}

internal sealed class ServerRow : StableRow<ServerItem>
{
    public ServerRow(ServerItem model) : base(model) { }
    public string Address => Model.Address;
    public ushort Port => Model.Port;
    public string Name => Model.Name;
    public string Endpoint => Model.Endpoint;
    public string UsersText => Model.UsersText;
    public string FilesText => Model.FilesText;
    public string PingText => Model.PingText;

    protected override void OnUpdated(ServerItem previous, ServerItem next)
    {
        Changed(previous.Name, next.Name, nameof(Name));
        Changed(previous.Endpoint, next.Endpoint, nameof(Endpoint));
        Changed(previous.UsersText, next.UsersText, nameof(UsersText));
        Changed(previous.FilesText, next.FilesText, nameof(FilesText));
        Changed(previous.PingText, next.PingText, nameof(PingText));
    }
}

internal sealed class SharedRow : StableRow<SharedFile>
{
    public SharedRow(SharedFile model) : base(model) { }
    public string Hash => Model.Hash;
    public string Name => Model.Name;
    public string Path => Model.Path;
    public ulong Size => Model.Size;
    public string SizeText => Model.SizeText;
    public string FolderText => Model.FolderText;
    public ulong Requests => Model.Requests;
    public string Ed2kLink => Model.Ed2kLink;

    protected override void OnUpdated(SharedFile previous, SharedFile next)
    {
        Changed(previous.Name, next.Name, nameof(Name));
        Changed(previous.Size, next.Size, nameof(Size));
        Changed(previous.SizeText, next.SizeText, nameof(SizeText));
        Changed(previous.FolderText, next.FolderText, nameof(FolderText));
        Changed(previous.Requests, next.Requests, nameof(Requests));
        Changed(previous.Ed2kLink, next.Ed2kLink, nameof(Ed2kLink));
    }
}
