using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TCP_Messages;

/// <summary>
/// Represents a user-defined TCP command configuration,
/// storing the connection target and the payload to transmit.
/// </summary>
public class TcpCommand
{
    /// <summary>
    /// Stable identifier used to correlate this command with its corresponding
    /// <see cref="Microsoft.Maui.ApplicationModel.AppAction"/>.
    /// Generated once at creation time and persisted alongside the command.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display label shown on the command button.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Target IP address or hostname for the TCP connection.</summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>Target TCP port number.</summary>
    public int Port { get; set; }

    /// <summary>Raw string payload sent over the TCP stream.</summary>
    public string Command { get; set; } = string.Empty;
}

/// <summary>
/// Main page of the application. Allows users to create, edit, delete,
/// and fire named TCP commands through a dynamically generated button interface.
/// Commands are persisted across sessions via <see cref="Preferences"/>.
/// </summary>
public partial class MainPage : ContentPage
{
    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    /// <summary>
    /// In-memory collection of all currently saved TCP commands.
    /// Kept in sync with persistent storage after every mutation.
    /// </summary>
    private List<TcpCommand> _savedCommands = new();

    /// <summary>
    /// Set of <see cref="TcpCommand.Id"/> values whose commands have been
    /// pinned as home-screen shortcuts via <see cref="AppActions"/>.
    /// Persisted separately from the command list.
    /// </summary>
    private HashSet<string> _pinnedIds = new();

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initialises the page, wires up XAML-generated controls,
    /// and restores any previously persisted commands.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        LoadPinnedIds();
        LoadCommands();

        // Subscribe to the static AppActions event so that tapping a
        // home-screen shortcut while the app is running fires the TCP command
        // immediately, without the user having to navigate back to this page.
        AppActions.OnAppAction += OnAppActionActivated;
    }

    // -------------------------------------------------------------------------
    // UI event handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Toggles the visibility of the input panel used to add new commands.
    /// </summary>
    private void OnTogglePanelClicked(object? sender, EventArgs? e)
    {
        InputPanel.IsVisible = !InputPanel.IsVisible;
    }

    /// <summary>
    /// Validates the user's input, creates a new <see cref="TcpCommand"/>,
    /// persists it, and appends the corresponding button row to the UI.
    /// </summary>
    private async void OnAddButtonClicked(object? sender, EventArgs? e)
    {
        // Guard: all fields must be filled before proceeding.
        if (string.IsNullOrWhiteSpace(IpEntry.Text) ||
            string.IsNullOrWhiteSpace(PortEntry.Text) ||
            string.IsNullOrWhiteSpace(CommandEntry.Text) ||
            string.IsNullOrWhiteSpace(LabelEntry.Text))
        {
            return;
        }

        // Guard: port must be a valid integer.
        if (!int.TryParse(PortEntry.Text, out int port))
        {
            await this.DisplayAlertAsync("Error", "Invalid port number.", "OK");
            return;
        }

        var newCmd = new TcpCommand
        {
            Label = LabelEntry.Text,
            Ip = IpEntry.Text,
            Port = port,
            Command = CommandEntry.Text
        };

        _savedCommands.Add(newCmd);
        SaveCommands();
        AddButtonToInterface(newCmd);

        // Reset transient input fields and collapse the panel.
        LabelEntry.Text = string.Empty;
        CommandEntry.Text = string.Empty;
        InputPanel.IsVisible = false;
    }

    // -------------------------------------------------------------------------
    // Dynamic UI construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a three-column button row for the given <paramref name="cmd"/>
    /// and appends it to <c>ButtonsContainer</c>.
    /// The row contains:
    /// <list type="bullet">
    ///   <item>A main action button that sends the TCP command.</item>
    ///   <item>An edit button that pre-populates the input panel.</item>
    ///   <item>A delete button that removes the command after confirmation.</item>
    /// </list>
    /// </summary>
    /// <param name="cmd">The command whose UI row should be created.</param>
    private void AddButtonToInterface(TcpCommand cmd)
    {
        // Four-column layout: [action | edit | pin | delete]
        var rowGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(5)
        };

        // --- Main action button -------------------------------------------
        var mainBtn = new Button
        {
            Text = cmd.Label,
            HorizontalOptions = LayoutOptions.Fill
        };

        // Respect the app's light/dark theme for the button colours.
        mainBtn.SetAppThemeColor(Button.BackgroundColorProperty,
            light: Colors.SlateGray,
            dark: Color.FromArgb("#2B2B2B"));
        mainBtn.SetAppThemeColor(Button.TextColorProperty,
            light: Colors.White,
            dark: Colors.White);

        mainBtn.Clicked += async (_, _) =>
        {
            // Disable the button for the duration of the operation to prevent
            // multiple concurrent TCP calls to the same target.
            mainBtn.IsEnabled = false;

            // Capture the current background colour before overwriting it so
            // we can restore the original value afterwards.  BackgroundColor
            // may be null when the button hasn't been rendered yet; fall back
            // to transparent in that unlikely case.
            Color originalColor = mainBtn.BackgroundColor ?? Colors.Transparent;

            (bool success, string? response) = await SendTcpCommandAsync(cmd.Ip, cmd.Port, cmd.Command);

            // Provide brief visual feedback: green on success, red on failure.
            mainBtn.BackgroundColor = success ? Colors.Green : Colors.Red;
            await Task.Delay(TimeSpan.FromMilliseconds(1500));

            mainBtn.BackgroundColor = originalColor;
            mainBtn.IsEnabled = true;

            // Show the server's response (or a failure notice) in a dialog.
            // When the call succeeded but the server sent no data, report that
            // explicitly rather than displaying an empty alert body.
            string alertTitle = success ? "Response" : "Error";
            string alertBody = success
                ? (string.IsNullOrWhiteSpace(response) ? "(no response from server)" : response)
                : "The command could not be delivered. Check the connection settings.";

            await this.DisplayAlertAsync(alertTitle, alertBody, "OK");
        };

        // --- Edit button --------------------------------------------------
        var editBtn = new Button
        {
            Text = "✎",
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Orange,
            TextColor = Colors.White,
            WidthRequest = 45,
            Margin = new Thickness(5, 0)
        };
        editBtn.Clicked += (_, _) => PrepareEdit(cmd, rowGrid);

        // --- Pin button ---------------------------------------------------
        // Reflects whether the command is currently registered as a
        // home-screen shortcut.  Appearance updates on every toggle.
        bool isPinned = _pinnedIds.Contains(cmd.Id);
        var pinBtn = new Button
        {
            Text = isPinned ? "★" : "☆",
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = isPinned ? Colors.SteelBlue : Colors.Gray,
            TextColor = Colors.White,
            WidthRequest = 45,
            Margin = new Thickness(5, 0)
        };
        pinBtn.Clicked += async (_, _) =>
        {
            pinBtn.IsEnabled = false;
            await ToggleAppActionAsync(cmd, pinBtn);
            pinBtn.IsEnabled = true;
        };

        // --- Delete button ------------------------------------------------
        var deleteBtn = new Button
        {
            Text = "✕",
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Red,
            TextColor = Colors.White,
            WidthRequest = 45
        };
        deleteBtn.Clicked += async (_, _) =>
        {
            bool confirmed = await this.DisplayAlertAsync(
                "Confirm deletion",
                $"Do you want to remove \"{cmd.Label}\"?",
                "Yes", "No");

            if (!confirmed)
                return;

            // Also remove any associated home-screen shortcut.
            if (_pinnedIds.Remove(cmd.Id))
            {
                SavePinnedIds();
                await SyncAppActionsAsync();
            }

            _savedCommands.Remove(cmd);
            SaveCommands();
            ButtonsContainer.Children.Remove(rowGrid);
        };

        // Assign controls to grid columns.
        Grid.SetColumn(mainBtn, 0);
        Grid.SetColumn(editBtn, 1);
        Grid.SetColumn(pinBtn, 2);
        Grid.SetColumn(deleteBtn, 3);

        rowGrid.Children.Add(mainBtn);
        rowGrid.Children.Add(editBtn);
        rowGrid.Children.Add(pinBtn);
        rowGrid.Children.Add(deleteBtn);

        ButtonsContainer.Children.Add(rowGrid);
    }

    /// <summary>
    /// Populates the input panel with the values of an existing command so the
    /// user can modify them, then removes the old row from the UI and its
    /// backing data so that <see cref="OnAddButtonClicked"/> will re-create
    /// it cleanly when the user saves.
    /// </summary>
    /// <param name="cmd">Command whose data should be loaded into the form.</param>
    /// <param name="rowToRemove">The grid row associated with <paramref name="cmd"/>.</param>
    private void PrepareEdit(TcpCommand cmd, Grid rowToRemove)
    {
        LabelEntry.Text = cmd.Label;
        IpEntry.Text = cmd.Ip;
        PortEntry.Text = cmd.Port.ToString();
        CommandEntry.Text = cmd.Command;

        // Remove the existing entry so it is not duplicated when saved again.
        _savedCommands.Remove(cmd);
        ButtonsContainer.Children.Remove(rowToRemove);

        InputPanel.IsVisible = true;
    }

    // -------------------------------------------------------------------------
    // Networking
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens a TCP connection to the specified host and port, transmits
    /// <paramref name="command"/> terminated by a newline character, and
    /// awaits a single-line response.  The entire operation is bounded by a
    /// five-second cancellation timeout.
    /// </summary>
    /// <param name="ip">Target IP address or hostname.</param>
    /// <param name="port">Target TCP port number.</param>
    /// <param name="command">Payload string to transmit (LF appended automatically).</param>
    /// <returns>
    /// A tuple where <c>Success</c> is <see langword="true"/> when the round-trip
    /// completed without error, and <c>Response</c> contains the first line
    /// returned by the remote host, or <see langword="null"/> if the stream
    /// closed before a newline was received or an error occurred.
    /// </returns>
    private static async Task<(bool Success, string? Response)> SendTcpCommandAsync(
        string ip, int port, string command)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await client.ConnectAsync(ip, port, cts.Token);

            await using NetworkStream stream = client.GetStream();

            byte[] payload = Encoding.UTF8.GetBytes(command + "\n");
            await stream.WriteAsync(payload, cts.Token);
            await stream.FlushAsync(cts.Token);

            // ReadLineAsync returns null when the stream reaches EOF before a
            // newline; we still consider the operation successful since the
            // command was transmitted, and surface null to the caller as-is.
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? response = await reader.ReadLineAsync(cts.Token);

            return (true, response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TCP] Error: {ex.Message}");
            return (false, null);
        }
    }

    // -------------------------------------------------------------------------
    // App Actions (home-screen shortcuts)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Toggles the home-screen shortcut for <paramref name="cmd"/>: pins it if
    /// it is not currently registered, or unpins it if it is.
    /// Updates <paramref name="pinBtn"/>'s appearance to reflect the new state.
    /// </summary>
    /// <param name="cmd">The TCP command to pin or unpin.</param>
    /// <param name="pinBtn">The button whose visual state must be refreshed.</param>
    private async Task ToggleAppActionAsync(TcpCommand cmd, Button pinBtn)
    {
        if (!AppActions.Current.IsSupported)
        {
            await this.DisplayAlertAsync(
                "Not supported",
                "Home-screen shortcuts are not available on this device.",
                "OK");
            return;
        }

        bool nowPinned;

        if (_pinnedIds.Contains(cmd.Id))
        {
            _pinnedIds.Remove(cmd.Id);
            nowPinned = false;
        }
        else
        {
            _pinnedIds.Add(cmd.Id);
            nowPinned = true;
        }

        SavePinnedIds();
        await SyncAppActionsAsync();

        // Refresh the button to match the new pinned state.
        pinBtn.Text = nowPinned ? "★" : "☆";
        pinBtn.BackgroundColor = nowPinned ? Colors.SteelBlue : Colors.Gray;
    }

    /// <summary>
    /// Rebuilds the full set of registered <see cref="AppAction"/> items from
    /// the commands whose IDs are present in <see cref="_pinnedIds"/>.
    /// Any command that no longer exists is silently skipped.
    /// </summary>
    private async Task SyncAppActionsAsync()
    {
        if (!AppActions.Current.IsSupported)
            return;

        try
        {
            IEnumerable<AppAction> actions = _savedCommands
                .Where(c => _pinnedIds.Contains(c.Id))
                .Select(c => new AppAction(c.Id, c.Label, icon: "shortcut_square"));

            await AppActions.Current.SetAsync(actions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppActions] Sync error: {ex.Message}");
        }
    }

    /// <summary>
    /// Invoked when the user activates a home-screen shortcut, either while
    /// the app is already running or immediately after it is brought to the
    /// foreground.  Finds the matching command and fires it over TCP.
    /// </summary>
    private async void OnAppActionActivated(object? sender, AppActionEventArgs e)
    {
        TcpCommand? cmd = _savedCommands.FirstOrDefault(c => c.Id == e.AppAction.Id);

        if (cmd is null)
            return;

        // Ensure UI interactions happen on the main thread.
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            (bool success, string? response) =
                await SendTcpCommandAsync(cmd.Ip, cmd.Port, cmd.Command);

            string alertTitle = success ? "Response" : "Error";
            string alertBody = success
                ? (string.IsNullOrWhiteSpace(response) ? "(no response from server)" : response)
                : "The command could not be delivered. Check the connection settings.";

            await this.DisplayAlertAsync(alertTitle, alertBody, "OK");
        });
    }

    /// <summary>
    /// Metodo chiamato dall'esterno (App.xaml.cs) quando viene attivata 
    /// una scorciatoia dalla Home.
    /// </summary>
    public void HandleExternalAction(string actionId)
    {
        if (_savedCommands.Count == 0)
        {
            LoadCommands();
        }

        // Cerchiamo se esiste un comando salvato con quell'ID
        TcpCommand? cmd = _savedCommands.FirstOrDefault(c => c.Id == actionId);

        if (cmd is null)
            return;

        // Eseguiamo il comando sulla UI Thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            (bool success, string? response) = await SendTcpCommandAsync(cmd.Ip, cmd.Port, cmd.Command);

            /*string alertTitle = success ? "Risposta CLOE" : "Errore";
            string alertBody = success
                ? (string.IsNullOrWhiteSpace(response) ? "(nessuna risposta)" : response)
                : "Comando non inviato. Verifica la connessione.";

            await this.DisplayAlertAsync(alertTitle, alertBody, "OK");*/
        });
    }


    // -------------------------------------------------------------------------
    // Persistence
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serialises the set of pinned command IDs to JSON and writes it to
    /// <see cref="Preferences"/> under a fixed key.
    /// </summary>
    private void SavePinnedIds()
    {
        string json = JsonSerializer.Serialize(_pinnedIds);
        Preferences.Default.Set("pinned_command_ids", json);
    }

    /// <summary>
    /// Restores the set of pinned command IDs from <see cref="Preferences"/>.
    /// Must be called before <see cref="LoadCommands"/> so that pin button
    /// states are correct when the UI is first built.
    /// </summary>
    private void LoadPinnedIds()
    {
        string json = Preferences.Default.Get("pinned_command_ids", string.Empty);

        if (string.IsNullOrEmpty(json))
            return;

        _pinnedIds = JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
    }


    /// <summary>
    /// Serialises the current command list to JSON and writes it to
    /// <see cref="Preferences"/> under a fixed key.
    /// </summary>
    private void SaveCommands()
    {
        string json = JsonSerializer.Serialize(_savedCommands);
        Preferences.Default.Set("saved_tcp_commands", json);
    }

    /// <summary>
    /// Reads the previously persisted JSON from <see cref="Preferences"/>,
    /// deserialises it, and rebuilds the button interface for each command.
    /// If no data is found, or deserialisation fails, the list remains empty.
    /// </summary>
    private void LoadCommands()
    {
        string json = Preferences.Default.Get("saved_tcp_commands", string.Empty);

        if (string.IsNullOrEmpty(json))
            return;

        // Deserialise defensively; null result is treated as an empty list.
        _savedCommands = JsonSerializer.Deserialize<List<TcpCommand>>(json) ?? new List<TcpCommand>();

        foreach (TcpCommand cmd in _savedCommands)
            AddButtonToInterface(cmd);
    }
}