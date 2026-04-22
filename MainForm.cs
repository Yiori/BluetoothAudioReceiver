using System;
using System.Drawing;
using System.Windows.Forms;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BluetoothAudioReceiver;

public class MainForm : Form
{
    // Controles UI
    private ListBox deviceListBox = null!;
    private Button connectButton = null!;
    private Button refreshButton = null!;
    private Label statusLabel = null!;
    private Label hintLabel = null!;
    private NotifyIcon trayIcon = null!;
    private ContextMenuStrip trayMenu = null!;

    // Estado Bluetooth
    private DeviceWatcher? deviceWatcher;
    private readonly ConcurrentDictionary<string, DeviceItem> devicesDictionary = new();
    private AudioPlaybackConnection? currentConnection;

    public MainForm()
    {
        SuspendLayout();
        BuildUI();
        ResumeLayout(false);
        PerformLayout();

        Load        += MainForm_Load;
        FormClosing += MainForm_FormClosing;
        Resize      += MainForm_Resize;
    }

    // ─────────────────────────────────────────────────────────────────
    // LAYOUT: TableLayoutPanel garante posicionamento correto
    // ─────────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        Text            = "Bluetooth Audio Receiver";
        Size            = new Size(460, 380);
        MinimumSize     = new Size(380, 300);
        StartPosition   = FormStartPosition.CenterScreen;

        // ── Status bar ──────────────────────────────────────────────
        statusLabel = new Label
        {
            Text      = "Iniciando...",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            Padding   = new Padding(4, 0, 0, 0)
        };

        hintLabel = new Label
        {
            Text      = "Dispositivos: 0",
            Dock      = DockStyle.Right,
            Width     = 140,
            TextAlign = ContentAlignment.MiddleRight,
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.Gray,
            Padding   = new Padding(0, 0, 4, 0)
        };

        var statusPanel = new System.Windows.Forms.Panel
        {
            Dock    = DockStyle.Fill,
            Padding = new Padding(0)
        };
        statusPanel.Controls.Add(statusLabel);
        statusPanel.Controls.Add(hintLabel);

        // ── Lista de dispositivos ────────────────────────────────────
        deviceListBox = new ListBox
        {
            Dock          = DockStyle.Fill,
            Font          = new Font("Segoe UI", 10),
            IntegralHeight= false,
            BorderStyle   = BorderStyle.None
        };

        // ── Botões ───────────────────────────────────────────────────
        connectButton = new Button
        {
            Text   = "Conectar",
            Dock   = DockStyle.Fill,
            Font   = new Font("Segoe UI", 10),
            Height = 38
        };
        connectButton.Click += ConnectButton_Click;

        refreshButton = new Button
        {
            Text   = "Atualizar",
            Dock   = DockStyle.Right,
            Width  = 110,
            Font   = new Font("Segoe UI", 10),
            Height = 38
        };
        refreshButton.Click += async (s, e) => await RefreshDevicesAsync();

        var buttonPanel = new System.Windows.Forms.Panel { Dock = DockStyle.Fill };
        buttonPanel.Controls.Add(connectButton);
        buttonPanel.Controls.Add(refreshButton);

        // ── TableLayoutPanel (3 linhas: status | lista | botoes) ─────
        var layout = new TableLayoutPanel
        {
            Dock       = DockStyle.Fill,
            RowCount   = 3,
            ColumnCount= 1,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,  46)); // status
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,  100)); // lista
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,  48)); // botoes

        layout.Controls.Add(statusPanel,  0, 0);
        layout.Controls.Add(deviceListBox,0, 1);
        layout.Controls.Add(buttonPanel,  0, 2);

        Controls.Add(layout);

        // ── System Tray ──────────────────────────────────────────────
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Restaurar", null, OnTrayRestoreClick);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Sair", null, OnTrayExitClick);

        trayIcon = new NotifyIcon
        {
            Text             = "Audio Receiver",
            Icon             = SystemIcons.Information,
            ContextMenuStrip = trayMenu,
            Visible          = true
        };
        trayIcon.DoubleClick += OnTrayRestoreClick;
    }

    // ─────────────────────────────────────────────────────────────────
    // INICIALIZAÇÃO
    // ─────────────────────────────────────────────────────────────────
    private async void MainForm_Load(object? sender, EventArgs e)
    {
        StartDeviceWatcher();
        await RefreshDevicesAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // DESCOBERTA: usa APENAS o seletor A2DP oficial
    // ─────────────────────────────────────────────────────────────────
    private async Task RefreshDevicesAsync()
    {
        SetStatus("Buscando dispositivos...");
        refreshButton.Enabled = false;
        devicesDictionary.Clear();

        try
        {
            // Seletor correto para AudioPlaybackConnection
            var selector = AudioPlaybackConnection.GetDeviceSelector();
            var found = await DeviceInformation.FindAllAsync(selector);

            foreach (var dev in found)
            {
                devicesDictionary[dev.Id] = new DeviceItem
                {
                    Id   = dev.Id,
                    Name = string.IsNullOrWhiteSpace(dev.Name) ? $"(dispositivo {dev.Id[..8]})" : dev.Name
                };
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Erro na busca: {ex.Message}");
        }
        finally
        {
            UpdateDeviceList();
            refreshButton.Enabled = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // WATCHER: monitora chegadas/saídas em tempo real
    // ─────────────────────────────────────────────────────────────────
    private void StartDeviceWatcher()
    {
        var selector = AudioPlaybackConnection.GetDeviceSelector();
        deviceWatcher = DeviceInformation.CreateWatcher(selector);

        deviceWatcher.Added += (_, args) =>
        {
            devicesDictionary[args.Id] = new DeviceItem
            {
                Id   = args.Id,
                Name = string.IsNullOrWhiteSpace(args.Name) ? $"(dispositivo {args.Id[..8]})" : args.Name
            };
            UpdateDeviceList();
        };

        deviceWatcher.Removed += (_, args) =>
        {
            devicesDictionary.TryRemove(args.Id, out DeviceItem? _);
            UpdateDeviceList();
        };

        deviceWatcher.Updated += (_, _) => UpdateDeviceList();
        deviceWatcher.Start();
    }

    // ─────────────────────────────────────────────────────────────────
    // UI helpers
    // ─────────────────────────────────────────────────────────────────
    private void UpdateDeviceList()
    {
        Invoke(() =>
        {
            var selectedId = (deviceListBox.SelectedItem as DeviceItem)?.Id;
            deviceListBox.Items.Clear();

            DeviceItem? toReselect = null;
            foreach (var item in devicesDictionary.Values)
            {
                deviceListBox.Items.Add(item);
                if (item.Id == selectedId) toReselect = item;
            }

            if (toReselect != null)
                deviceListBox.SelectedItem = toReselect;

            hintLabel.Text = $"Dispositivos: {deviceListBox.Items.Count}";

            if (currentConnection == null)
            {
                SetStatus(deviceListBox.Items.Count == 0
                    ? "Nenhum dispositivo A2DP encontrado. Clique em Atualizar."
                    : "Selecione um dispositivo e clique em Conectar.");
            }
        });
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
            Invoke(() => statusLabel.Text = text);
        else
            statusLabel.Text = text;
    }

    // ─────────────────────────────────────────────────────────────────
    // CONEXÃO A2DP
    // ─────────────────────────────────────────────────────────────────
    private async void ConnectButton_Click(object? sender, EventArgs e)
    {
        if (currentConnection != null)
        {
            DisconnectCurrent();
            return;
        }

        if (deviceListBox.SelectedItem is not DeviceItem selected)
        {
            MessageBox.Show("Selecione um dispositivo da lista.", "Aviso",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SetStatus($"Conectando a {selected.Name}...");
            connectButton.Enabled = false;

            currentConnection = AudioPlaybackConnection.TryCreateFromId(selected.Id);

            if (currentConnection == null)
            {
                ShowError("Nao foi possivel criar a conexao de audio.\n" +
                          "Verifique se o dispositivo suporta A2DP.");
                ResetUIState();
                return;
            }

            currentConnection.StateChanged += Connection_StateChanged;
            await currentConnection.StartAsync();
            var result = await currentConnection.OpenAsync();

            if (result.Status != AudioPlaybackConnectionOpenResultStatus.Success)
            {
                HandleOpenError(result.Status, selected.Name);
                DisconnectCurrent();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Excecao ao conectar:\n{ex.Message}");
            DisconnectCurrent();
        }
    }

    private void HandleOpenError(AudioPlaybackConnectionOpenResultStatus status, string deviceName)
    {
        string msg = status switch
        {
            AudioPlaybackConnectionOpenResultStatus.DeniedBySystem =>
                $"O sistema negou a conexao com '{deviceName}' (DeniedBySystem).\n\n" +
                "Este erro ocorre quando o app nao tem Package Identity com a\n" +
                "capability 'bluetooth'. Para corrigir:\n\n" +
                "  1. Execute o script Setup-And-Install.ps1 (como Administrador)\n" +
                "     para criar o certificado e instalar o app como MSIX.\n\n" +
                "  2. Abra o app instalado (pelo menu Iniciar) em vez do .exe diretamente.",

            AudioPlaybackConnectionOpenResultStatus.RequestTimedOut =>
                "Tempo esgotado. O dispositivo pode estar fora de alcance.",

            AudioPlaybackConnectionOpenResultStatus.UnknownFailure =>
                "Falha desconhecida. Tente reiniciar o servico Bluetooth do Windows\n" +
                "(services.msc → Bluetooth Support Service → Reiniciar).",

            _ => $"Falha ao abrir conexao. Status: {status}"
        };

        MessageBox.Show(msg, $"Falha A2DP — {status}",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ShowError(string msg) =>
        MessageBox.Show(msg, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void DisconnectCurrent()
    {
        if (currentConnection != null)
        {
            currentConnection.StateChanged -= Connection_StateChanged;
            currentConnection.Dispose();
            currentConnection = null;
        }
        ResetUIState();
    }

    private void ResetUIState()
    {
        if (InvokeRequired) { Invoke(ResetUIState); return; }
        SetStatus(deviceListBox.Items.Count > 0
            ? "Desconectado. Selecione um dispositivo."
            : "Desconectado.");
        connectButton.Text    = "Conectar";
        connectButton.Enabled = true;
        trayIcon.Text         = "Audio Receiver";
    }

    private void Connection_StateChanged(AudioPlaybackConnection sender, object args)
    {
        Invoke(() =>
        {
            switch (sender.State)
            {
                case AudioPlaybackConnectionState.Opened:
                    SetStatus("Conectado — reproduzindo audio Bluetooth.");
                    connectButton.Text    = "Desconectar";
                    connectButton.Enabled = true;
                    trayIcon.Text         = "Audio Receiver — Conectado";
                    break;

                case AudioPlaybackConnectionState.Closed:
                    trayIcon.Text = "Audio Receiver";
                    DisconnectCurrent();
                    break;
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // SYSTEM TRAY
    // ─────────────────────────────────────────────────────────────────
    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            trayIcon.ShowBalloonTip(1500, "Bluetooth Audio Receiver",
                "Rodando na bandeja do sistema.", ToolTipIcon.Info);
        }
    }

    private void OnTrayRestoreClick(object? sender, EventArgs e)
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OnTrayExitClick(object? sender, EventArgs e) => Application.Exit();

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        DisconnectCurrent();
        deviceWatcher?.Stop();
        deviceWatcher = null;
        trayIcon.Visible = false;
        trayIcon.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────
    // Classe auxiliar do ListBox
    // ─────────────────────────────────────────────────────────────────
    private class DeviceItem
    {
        public string Id   { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }
}
