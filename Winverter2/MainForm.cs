using System.Diagnostics;
using System.Security.Principal;

namespace Winverter2;

public partial class MainForm : Form
{
    private readonly ListView applications = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, HideSelection = false };
    private readonly CheckBox currentUser = new() { Text = "Only processes owned by the current user", Checked = true, AutoSize = true };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(10) };
    private readonly Dictionary<nint, InversionOverlay> overlays = new();
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 33 };
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 2000 };
    private readonly Dictionary<int, (Process Process, nint Handle)> selected = new();
    private readonly ListView alwaysApplications = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, HideSelection = false };
    private readonly AlwaysInvertStore store = new();
    private readonly HashSet<string> alwaysPaths = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<int> automaticProcesses = [];
    private readonly Button stopButton = new() { Text = "Stop inversion", AutoSize = true };
    private bool paused;
    private bool updating, magnificationReady;
    private readonly string? currentSid = GetCurrentSid();

    private static string? GetCurrentSid() { using var identity = WindowsIdentity.GetCurrent(); return identity.User?.Value; }

    public MainForm()
    {
        InitializeComponent();
        Text = "Winverter — application colour inversion";
        MinimumSize = new Size(900, 620);
        ClientSize = new Size(1000, 680);
        applications.Columns.Add("Application", 190);
        applications.Columns.Add("PID", 75);
        applications.Columns.Add("Windows", 80);
        applications.Columns.Add("Window titles", 420);
        var refreshButton = new Button { Text = "Refresh", AutoSize = true };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(10), AutoSize = true };
        toolbar.Controls.AddRange([currentUser, refreshButton, stopButton]);
        var addButton = new Button { Text = "Add selected to Always Invert", AutoSize = true };
        var browseButton = new Button { Text = "Add executable…", AutoSize = true };
        var removeButton = new Button { Text = "Remove selected", AutoSize = true };
        var alwaysToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        alwaysToolbar.Controls.AddRange([addButton, browseButton, removeButton]);
        alwaysApplications.Columns.Add("Executable", 210);
        alwaysApplications.Columns.Add("Full path", 550);
        var manualGroup = new GroupBox { Text = "Running applications — select to invert (Ctrl / Shift for multiple)", Dock = DockStyle.Fill, Padding = new Padding(8) };
        manualGroup.Controls.Add(applications);
        var alwaysGroup = new GroupBox { Text = "Always Invert — saved automatically; matching processes are hidden above", Dock = DockStyle.Fill, Padding = new Padding(8) };
        alwaysGroup.Controls.Add(alwaysApplications);
        alwaysGroup.Controls.Add(alwaysToolbar);
        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        lists.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        lists.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        lists.Controls.Add(manualGroup, 0, 0);
        lists.Controls.Add(alwaysGroup, 0, 1);
        Controls.Add(lists);
        Controls.Add(toolbar);
        Controls.Add(status);
        DarkTheme.Apply(this);
        refreshButton.Click += (_, _) => RefreshApplications();
        stopButton.Click += (_, _) =>
        {
            paused = !paused;
            stopButton.Text = paused ? "Resume inversion" : "Stop inversion";
            if (paused) StopInversion(); else RefreshApplications();
        };
        addButton.Click += (_, _) => AddSelectedToAlways();
        browseButton.Click += (_, _) => BrowseExecutable();
        removeButton.Click += (_, _) => SaveAlwaysPaths(alwaysPaths.Except(alwaysApplications.SelectedItems.Cast<ListViewItem>().Select(row => (string)row.Tag!), StringComparer.OrdinalIgnoreCase));
        currentUser.CheckedChanged += (_, _) => RefreshApplications();
        applications.SelectedIndexChanged += (_, _) => SelectApplications();
        animation.Tick += (_, _) => UpdateInversion();
        refresh.Tick += (_, _) => RefreshApplications();
        Shown += (_, _) =>
        {
            magnificationReady = Native.MagInitialize();
            try { alwaysPaths.UnionWith(store.Load().Where(path => !IsOwnExecutable(path))); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            { MessageBox.Show(this, $"Could not load Always Invert settings. The saved file has been left unchanged.\n\n{ex.Message}", "Settings could not be loaded", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            RefreshAlwaysList();
            StopInversion();
            RefreshApplications();
            refresh.Start(); animation.Start();
        };
        FormClosed += (_, _) =>
        {
            animation.Dispose(); refresh.Dispose(); StopInversion();
            if (magnificationReady) Native.MagUninitialize();
        };
    }

    private bool OwnedByCurrentUser(int pid)
    {
        nint process = Native.OpenProcess(0x1000, false, (uint)pid);
        if (process == 0) return false;
        try
        {
            if (!Native.OpenProcessToken(process, 8, out var token)) return false;
            try { using var identity = new WindowsIdentity(token); return currentSid != null && identity.User?.Value == currentSid; }
            finally { Native.CloseHandle(token); }
        }
        catch (System.Security.SecurityException) { return false; }
        finally { Native.CloseHandle(process); }
    }

    private void RefreshApplications()
    {
        var windows = Native.Windows(includeMinimized: true).Where(w => w.ProcessId != Environment.ProcessId).GroupBy(w => w.ProcessId).ToDictionary(g => g.Key, g => g.ToArray());
        var rows = new List<ListViewItem>();
        var automatic = new HashSet<int>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (!windows.TryGetValue(process.Id, out var open) || (currentUser.Checked && !OwnedByCurrentUser(process.Id))) continue;
                    var path = Native.ExecutablePath(process.Id);
                    if (IsOwnExecutable(path)) continue;
                    if (path != null && alwaysPaths.Contains(path)) { automatic.Add(process.Id); continue; }
                    rows.Add(new ListViewItem([process.ProcessName, process.Id.ToString(), open.Length.ToString(), string.Join(" | ", open.Select(w => w.Title).Where(t => t.Length > 0).Distinct())]) { Tag = process.Id });
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException) { }
            }
        }
        // Removing a saved rule restores its processes rather than selecting them manually.
        foreach (var pid in automaticProcesses.Except(automatic).ToArray()) RemoveProcess(pid);
        automaticProcesses = automatic;
        var topItem = applications.TopItem;
        int topIndex = topItem?.Index ?? 0;
        object? topProcess = topItem?.Tag;
        object? focusedProcess = applications.FocusedItem?.Tag;
        updating = true; applications.BeginUpdate();
        try
        {
            applications.Items.Clear();
            applications.Items.AddRange(rows.OrderBy(r => r.Text, StringComparer.CurrentCultureIgnoreCase).ToArray());
            foreach (ListViewItem row in applications.Items)
            {
                row.Selected = selected.ContainsKey((int)row.Tag!);
                row.Focused = Equals(row.Tag, focusedProcess);
            }
        }
        finally { applications.EndUpdate(); updating = false; }
        // Follow the same top row when processes are inserted/removed above it.
        // If it disappeared, keep the nearest remaining row at the previous index.
        if (applications.Items.Count > 0)
            applications.TopItem = applications.Items.Cast<ListViewItem>().FirstOrDefault(row => Equals(row.Tag, topProcess))
                ?? applications.Items[Math.Min(topIndex, applications.Items.Count - 1)];
        SelectApplications();
    }

    private void SelectApplications()
    {
        if (updating || !magnificationReady) return;
        var requested = applications.SelectedItems.Cast<ListViewItem>().Select(row => (int)row.Tag!).ToHashSet();
        requested.UnionWith(automaticProcesses);
        requested.Remove(Environment.ProcessId); // Never allow our own UI or overlays to be inverted.
        if (paused) requested.Clear();
        foreach (var pid in selected.Keys.Where(pid => !requested.Contains(pid)).ToArray()) RemoveProcess(pid);
        string? error = null;
        foreach (var pid in requested.Where(pid => !selected.ContainsKey(pid)))
        {
            Process? process = null;
            nint handle = 0;
            try
            {
                process = Process.GetProcessById(pid);
                // Retain the original process instance without requesting full process access.
                handle = Native.OpenProcess(0x101000, false, (uint)pid);
                if (handle == 0) throw new System.ComponentModel.Win32Exception("The process is protected or has exited.");
                selected.Add(pid, (process, handle));
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
            {
                process?.Dispose();
                if (handle != 0) Native.CloseHandle(handle);
                error = $"Cannot invert PID {pid}: {ex.Message}";
                updating = true;
                try { foreach (ListViewItem row in applications.Items) if ((int)row.Tag! == pid) row.Selected = false; }
                finally { updating = false; }
            }
        }
        UpdateStatus();
        UpdateInversion();
        if (error != null) status.Text = error;
    }

    private void UpdateInversion()
    {
        if (selected.Count == 0 && overlays.Count == 0) return;
        try
        {
            foreach (var entry in selected.ToArray())
                if (entry.Value.Process.HasExited) RemoveProcess(entry.Key);
            var overlayHandles = overlays.Values.Select(o => o.Handle).ToHashSet();
            var windows = Native.Windows().Where(w => !overlayHandles.Contains(w.Handle)).ToList();
            var targets = windows.Where(w => w.ProcessId != Environment.ProcessId && selected.ContainsKey(w.ProcessId)).ToArray();
            var live = targets.Select(w => w.Handle).ToHashSet();
            foreach (var stale in overlays.Keys.Where(h => !live.Contains(h)).ToArray()) { overlays[stale].Dispose(); overlays.Remove(stale); }
            foreach (var target in targets)
                if (!overlays.ContainsKey(target.Handle)) overlays.Add(target.Handle, new InversionOverlay());
            var excluded = overlays.Values.Select(o => o.Handle).ToArray();
            foreach (var target in targets) overlays[target.Handle].Update(target, windows.TakeWhile(w => w.Handle != target.Handle), excluded);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { StopInversion(); status.Text = $"Inversion stopped: {ex.Message}"; }
    }

    private void StopInversion()
    {
        foreach (var overlay in overlays.Values) overlay.Dispose();
        overlays.Clear();
        foreach (var pid in selected.Keys.ToArray()) RemoveProcess(pid);
        UpdateStatus();
    }

    private void RemoveProcess(int pid)
    {
        if (!selected.Remove(pid, out var entry)) return;
        entry.Process.Dispose();
        Native.CloseHandle(entry.Handle);
        updating = true;
        try { foreach (ListViewItem row in applications.Items) if ((int)row.Tag! == pid) row.Selected = false; }
        finally { updating = false; }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        status.Text = !magnificationReady ? "Windows Magnification could not initialize. Inversion is unavailable."
            : paused ? "Inversion paused, including Always Invert. Click Resume inversion to continue."
            : selected.Count == 0 ? "Select applications to invert their windows. Use Ctrl+click or Shift+click for multiple selections."
            : $"Inverting {selected.Count} application(s), including {selected.Keys.Count(automaticProcesses.Contains)} Always Invert match(es). Stop inversion pauses all.";
    }

    private void RefreshAlwaysList()
    {
        alwaysApplications.BeginUpdate();
        try
        {
            alwaysApplications.Items.Clear();
            foreach (var path in alwaysPaths.Order(StringComparer.OrdinalIgnoreCase))
                alwaysApplications.Items.Add(new ListViewItem([Path.GetFileName(path), path]) { Tag = path });
        }
        finally { alwaysApplications.EndUpdate(); }
    }

    private void SaveAlwaysPaths(IEnumerable<string> paths)
    {
        try
        {
            var next = paths.Select(AlwaysInvertStore.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (next.Any(IsOwnExecutable)) throw new InvalidDataException("Winverter cannot add its own executable to Always Invert.");
            store.Save(next);
            alwaysPaths.Clear(); alwaysPaths.UnionWith(next);
            RefreshAlwaysList(); RefreshApplications();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { MessageBox.Show(this, $"The Always Invert list could not be saved. Your changes were not applied.\n\n{ex.Message}", "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void AddSelectedToAlways()
    {
        var paths = new List<string>();
        foreach (ListViewItem row in applications.SelectedItems)
        {
            var path = Native.ExecutablePath((int)row.Tag!);
            if (path == null)
            {
                MessageBox.Show(this, $"Cannot read the executable path for {row.Text}. The process may have exited or be protected. You can use Add executable instead.", "Executable unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            paths.Add(path);
        }
        if (paths.Count > 0) SaveAlwaysPaths(alwaysPaths.Concat(paths));
    }

    private void BrowseExecutable()
    {
        using var dialog = new OpenFileDialog { Title = "Choose an executable to always invert", Filter = "Applications (*.exe)|*.exe", Multiselect = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) SaveAlwaysPaths(alwaysPaths.Concat(dialog.FileNames));
    }

    private static bool IsOwnExecutable(string? path) => path != null &&
        string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
}
