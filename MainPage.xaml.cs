using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TCP_Messages;

// Classe per rappresentare i dati di un bottone
public class TcpCommand
{
    public string Label { get; set; }
    public string Ip { get; set; }
    public int Port { get; set; }
    public string Command { get; set; }
}

public partial class MainPage : ContentPage
{
    private List<TcpCommand> _savedCommands = new();

    public MainPage()
    {
        InitializeComponent();
        LoadCommands(); // Carica i bottoni all'avvio
    }

    private void OnTogglePanelClicked(object sender, EventArgs e)
    {
        InputPanel.IsVisible = !InputPanel.IsVisible;
    }

    private async void OnAddButtonClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IpEntry.Text) ||
            string.IsNullOrWhiteSpace(PortEntry.Text) ||
            string.IsNullOrWhiteSpace(CommandEntry.Text) ||
            string.IsNullOrWhiteSpace(LabelEntry.Text))
            return;

        if (!int.TryParse(PortEntry.Text, out int port))
        {
            await this.DisplayAlertAsync("Errore", "Porta non valida", "OK");
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

        // Reset e chiusura pannello
        LabelEntry.Text = CommandEntry.Text = string.Empty;
        InputPanel.IsVisible = false;
    }

    private void AddButtonToInterface(TcpCommand cmd)
    {
        // Creiamo una griglia per la riga: una colonna larga per il comando, due strette per le icone
        var rowGrid = new Grid
        {
            ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Auto }
        },
            Margin = new Thickness(5, 5)
        };

        var mainBtn = new Button
        {
            Text = cmd.Label,
            HorizontalOptions = LayoutOptions.Fill
        };

        mainBtn.SetAppThemeColor(Button.BackgroundColorProperty, Colors.SlateGray, Color.FromArgb("#2B2B2B"));
        mainBtn.SetAppThemeColor(Button.TextColorProperty, Colors.White, Colors.White);

        // Gestione del click con feedback
        mainBtn.Clicked += async (s, e) =>
        {
            mainBtn.IsEnabled = false; // Disabilita per evitare invii doppi
            var originalColor = mainBtn.BackgroundColor;

            bool success = await SendTcpCommand(cmd.Ip, cmd.Port, cmd.Command);

            if (success)
            {
                mainBtn.BackgroundColor = Colors.Green;
            }
            else
            {
                mainBtn.BackgroundColor = Colors.Red;
            }

            // Aspetta 1.5 secondi e poi ripristina il colore originale
            await Task.Delay(1500);
            mainBtn.BackgroundColor = originalColor;
            mainBtn.IsEnabled = true;
        };

        // 2. Bottone Modifica
        var editBtn = new Button
        {
            Text = "✎", // Icona o testo breve
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Orange,
            TextColor = Colors.White,
            WidthRequest = 45,
            Margin = new Thickness(5, 0)
        };
        editBtn.Clicked += (s, e) => PrepareEdit(cmd, rowGrid);

        // 3. Bottone Elimina
        var deleteBtn = new Button
        {
            Text = "X",
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Red,
            TextColor = Colors.White,
            WidthRequest = 45
        };
        deleteBtn.Clicked += async (s, e) =>
        {
            bool confirm = await this.DisplayAlertAsync("Conferma", $"Vuoi eliminare {cmd.Label}?", "Sì", "No");
            if (confirm)
            {
                _savedCommands.Remove(cmd);
                SaveCommands();
                ButtonsContainer.Children.Remove(rowGrid);
            }
        };

        // Aggiungiamo i controlli alla griglia nelle rispettive colonne
        Grid.SetColumn(mainBtn, 0);
        Grid.SetColumn(editBtn, 1);
        Grid.SetColumn(deleteBtn, 2);

        rowGrid.Children.Add(mainBtn);
        rowGrid.Children.Add(editBtn);
        rowGrid.Children.Add(deleteBtn);

        ButtonsContainer.Children.Add(rowGrid);
    }

    // Funzione per caricare i dati nel pannello superiore per la modifica
    private void PrepareEdit(TcpCommand cmd, Grid rowToRemove)
    {
        LabelEntry.Text = cmd.Label;
        IpEntry.Text = cmd.Ip;
        PortEntry.Text = cmd.Port.ToString();
        CommandEntry.Text = cmd.Command;

        // Rimuoviamo il vecchio record (verrà salvato come nuovo al click su "Salva")
        _savedCommands.Remove(cmd);
        ButtonsContainer.Children.Remove(rowToRemove);

        // Apriamo il pannello
        InputPanel.IsVisible = true;
    }

    private async Task<bool> SendTcpCommand(string ip, int port, string command)
    {
        try
        {
            using TcpClient client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await client.ConnectAsync(ip, port, cts.Token);

            using NetworkStream stream = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(command + "\n");

            await stream.WriteAsync(data, 0, data.Length, cts.Token);
            await stream.FlushAsync(cts.Token);

            return true; // Tutto ok
        }
        catch (Exception ex)
        {
            // Logghiamo l'errore ma restituiamo false per il feedback visivo
            System.Diagnostics.Debug.WriteLine($"Errore TCP: {ex.Message}");
            return false;
        }
    }

    private void SaveCommands()
    {
        string json = JsonSerializer.Serialize(_savedCommands);
        Preferences.Default.Set("saved_tcp_commands", json);
    }

    private void LoadCommands()
    {
        string json = Preferences.Default.Get("saved_tcp_commands", string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            _savedCommands = JsonSerializer.Deserialize<List<TcpCommand>>(json) ?? new();
            foreach (var cmd in _savedCommands)
            {
                AddButtonToInterface(cmd);
            }
        }
    }
}