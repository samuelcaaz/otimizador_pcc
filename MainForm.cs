using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OtimizadorPC
{
    public class MainForm : Form
    {
        // Paleta Dark Slate moderna
        private static readonly Color BgDark = Color.FromArgb(15, 23, 42);
        private static readonly Color CardBg = Color.FromArgb(30, 41, 59);
        private static readonly Color CardBorder = Color.FromArgb(51, 65, 85);
        private static readonly Color ConsoleBg = Color.FromArgb(9, 14, 26);
        private static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
        private static readonly Color AccentSky = Color.FromArgb(14, 165, 233);
        private static readonly Color AccentIndigo = Color.FromArgb(99, 102, 241);
        private static readonly Color AccentAmber = Color.FromArgb(245, 158, 11);
        private static readonly Color AccentRose = Color.FromArgb(225, 29, 72);
        private static readonly Color AccentSlate = Color.FromArgb(71, 85, 105);

        private Label _lblAdminBadge;
        private Label _lblDiskValue, _lblDiskSub;
        private Label _lblRamValue, _lblRamSub;
        private Label _lblSsdValue, _lblSsdSub;
        private Label _lblOsValue, _lblOsSub;

        // Checkboxes - Limpeza
        private CheckBox _chkTempLogs;
        private CheckBox _chkOrphanUpdaters;
        private CheckBox _chkDevShaders;
        private CheckBox _chkWinUpdateCache;
        private CheckBox _chkBrowserSpotify;
        private CheckBox _chkRecycleBin;

        // Checkboxes - Anti-SSD 100%
        private CheckBox _chkTrim;
        private CheckBox _chkAhciPower;
        private CheckBox _chkStorAhciMsi;
        private CheckBox _chkNtfsSsd;
        private CheckBox _chkFastStartupP2P;
        private CheckBox _chkShadowLimit;

        // Checkboxes - Diagnostico e Reparo
        private CheckBox _chkRestorePoint;
        private CheckBox _chkHealthAudit;
        private CheckBox _chkNetworkVBox;
        private CheckBox _chkChkdsk;
        private CheckBox _chkDismCleanup;
        private CheckBox _chkSfcScan;
        private CheckBox _chkDismRestore;

        // Botoes e progresso
        private Button _btnOptimize;
        private Button _btnDiagnose;
        private Button _btnDiskMap;
        private Button _btnOpenLogs;
        private Button _btnRestore;
        private Button _btnCancel;
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private RichTextBox _console;

        private OptimizerEngine _engine;
        private CancellationTokenSource _cts;
        private bool _isBusy;

        public MainForm()
        {
            Text = "PC Doctor Pro - Otimizador & Reparo Seguro do Windows";
            Size = new Size(1060, 740);
            MinimumSize = new Size(960, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgDark;
            ForeColor = TextPrimary;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            _engine = new OptimizerEngine(AppendLogThreadSafe, UpdateProgressThreadSafe);
            BuildUI();
            Load += delegate { RefreshStatsAsync(); };
        }

        private void BuildUI()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.Padding = new Padding(14, 12, 14, 12);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));   // Header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));   // Stats cards
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));  // Checkboxes
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));   // Action bar
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // Progress + status
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Console
            Controls.Add(root);

            // --- 1. HEADER ---
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            Label title = new Label
            {
                Text = "PC Doctor Pro  |  Otimizacao, Limpeza & Reparo Seguro",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(2, 2)
            };
            Label subtitle = new Label
            {
                Text = "Manutencao tecnica real (anti-SSD 100%, limpeza de lixo e reparo do Windows) — nunca apaga arquivos pessoais nem quebra recursos do sistema.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(3, 28)
            };
            _lblAdminBadge = new Label
            {
                Text = "VERIFICANDO...",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AccentSlate,
                AutoSize = false,
                Size = new Size(138, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(header.Width - 142, 8)
            };
            header.Resize += delegate { _lblAdminBadge.Left = header.Width - _lblAdminBadge.Width - 2; };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(_lblAdminBadge);
            root.Controls.Add(header, 0, 0);

            // --- 2. DASHBOARD CARDS ---
            TableLayoutPanel statsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 2, 0, 6)
            };
            for (int i = 0; i < 4; i++) statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            statsGrid.Controls.Add(CreateStatCard("DISCO C:", out _lblDiskValue, out _lblDiskSub), 0, 0);
            statsGrid.Controls.Add(CreateStatCard("MEMORIA RAM", out _lblRamValue, out _lblRamSub), 1, 0);
            statsGrid.Controls.Add(CreateStatCard("SAUDE SMART DO SSD", out _lblSsdValue, out _lblSsdSub), 2, 0);
            statsGrid.Controls.Add(CreateStatCard("SISTEMA OPERACIONAL", out _lblOsValue, out _lblOsSub), 3, 0);
            root.Controls.Add(statsGrid, 0, 1);

            // --- 3. MODULES GRID (3 COLUNAS) ---
            TableLayoutPanel modGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8)
            };
            for (int i = 0; i < 3; i++) modGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            ToolTip tip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 250 };

            // Coluna 1: Limpeza Segura
            Panel col1 = CreateModuleCard("1. LIMPEZA SEGURA (ESPACO)", AccentGreen);
            FlowLayoutPanel flow1 = CreateCheckFlow();
            _chkTempLogs = AddCheck(flow1, "Temporarios, CrashDumps e Logs CBS", true, tip, "Limpa %TEMP%, Windows\\Temp, dumps de erro e logs antigos.");
            _chkOrphanUpdaters = AddCheck(flow1, "Instaladores orfaos (*-updater, NVIDIA)", true, tip, "Remove instaladores antigos deixados por atualizacoes Electron, NVIDIA e WinRE.");
            _chkDevShaders = AddCheck(flow1, "Caches Dev e Shaders (uv, bun, DXCache)", true, tip, "Limpa caches 100% recriaveis de Python/uv, Bun, pip, npm, pnpm prune e DirectX/GPU.");
            _chkWinUpdateCache = AddCheck(flow1, "Cache do Windows Update e Delivery Opt.", true, tip, "Limpa downloads antigos/corrompidos do Windows Update (SoftwareDistribution).");
            _chkBrowserSpotify = AddCheck(flow1, "Cache de Navegadores e Spotify (opcional)", false, tip, "Limpa apenas arquivos de cache (nao apaga senhas, historico ou logins). Feche os apps antes.");
            _chkRecycleBin = AddCheck(flow1, "Esvaziar Lixeira do Windows (opcional)", false, tip, "Esvazia a Lixeira permanentemente. Desmarcado por seguranca.");
            col1.Controls.Add(flow1);
            flow1.BringToFront();
            modGrid.Controls.Add(col1, 0, 0);

            // Coluna 2: Anti-SSD 100%
            Panel col2 = CreateModuleCard("2. ANTI-SSD 100% & DESEMPENHO", AccentSky);
            FlowLayoutPanel flow2 = CreateCheckFlow();
            _chkTrim = AddCheck(flow2, "Executar TRIM no SSD (ReTrim)", true, tip, "Envia comando TRIM para recuperar velocidade de escrita e cache SLC do SSD.");
            _chkAhciPower = AddCheck(flow2, "Desativar economia SATA AHCI LPM / PCIe", true, tip, "Impede o SSD SATA de entrar em sono profundo (HIPM/DIPM) e travar a fila de I/O.");
            _chkStorAhciMsi = AddCheck(flow2, "Fix Microsoft StorAHCI (MSI-Mode)", true, tip, "Corrige o bug oficial do storahci.sys em chipsets Intel que congela o SSD em 100%.");
            _chkNtfsSsd = AddCheck(flow2, "Otimizar NTFS (menos escritas + RAM)", true, tip, "Desativa LastAccess/8.3 e ativa cache de metadados NTFS na RAM (memoryusage=2).");
            _chkFastStartupP2P = AddCheck(flow2, "Desativar Fast Startup e Update P2P", true, tip, "Evita carregar estado corrompido do kernel apos travamentos e uploads P2P do Windows Update.");
            _chkShadowLimit = AddCheck(flow2, "Limitar Shadow Storage em 4 GB (Volsnap)", true, tip, "Resolve erros Volsnap ID 36/25 e impede o VSS de consumir dezenas de GB.");
            col2.Controls.Add(flow2);
            flow2.BringToFront();
            modGrid.Controls.Add(col2, 1, 0);

            // Coluna 3: Diagnostico e Reparo
            Panel col3 = CreateModuleCard("3. DIAGNOSTICO & REPARO DE ERROS", AccentAmber);
            FlowLayoutPanel flow3 = CreateCheckFlow();
            _chkRestorePoint = AddCheck(flow3, "Criar Ponto de Restauracao antes (Backup)", true, tip, "Cria um Ponto de Restauracao do Windows em C:\\ antes de aplicar qualquer mudanca (seguranca extra).");
            _chkHealthAudit = AddCheck(flow3, "Auditoria SMART + Erros Event Viewer", true, tip, "Identifica erros criticos dos ultimos 7 dias (BSOD, Kernel-Power, disco, drivers).");
            _chkNetworkVBox = AddCheck(flow3, "Corrigir Rede, Portas TCP/UDP e VBoxNetLwf", true, tip, "Flush DNS, expande portas efemeras (fix Tcpip 4266/4231) e desativa filtro VBoxNetLwf.");
            _chkChkdsk = AddCheck(flow3, "Verificar erros no disco (chkdsk /scan)", true, tip, "Verificacao online e segura do sistema de arquivos NTFS (nao exige reinicio).");
            _chkDismCleanup = AddCheck(flow3, "Compactar WinSxS (DISM Cleanup)", true, tip, "Remove componentes obsoletos do Windows de forma segura para liberar GBs.");
            _chkSfcScan = AddCheck(flow3, "Reparar arquivos do sistema (SFC /scannow)", true, tip, "Verifica e restaura arquivos/DLLs corrompidos do Windows.");
            _chkDismRestore = AddCheck(flow3, "Reparo profundo (DISM /RestoreHealth ~10m)", false, tip, "Baixa arquivos saudaveis do Windows Update se houver corrupcao grave. Pode levar 10-15 min.");
            col3.Controls.Add(flow3);
            flow3.BringToFront();
            modGrid.Controls.Add(col3, 2, 0);

            root.Controls.Add(modGrid, 0, 2);

            // --- 4. ACTION BAR ---
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 2)
            };
            _btnOptimize = CreateButton("OTIMIZAR E REPARAR AGORA", AccentGreen, 235, true);
            _btnDiagnose = CreateButton("Apenas Diagnosticar", AccentSky, 165, false);
            _btnDiskMap = CreateButton("Mapa de Espaco (C:)", AccentIndigo, 165, false);
            _btnOpenLogs = CreateButton("Pasta de Logs", AccentSlate, 120, false);
            _btnRestore = CreateButton("Restaurar Padroes", AccentSlate, 140, false);
            _btnCancel = CreateButton("Cancelar", AccentRose, 95, false);
            _btnCancel.Enabled = false;

            _btnOptimize.Click += OnOptimizeClick;
            _btnDiagnose.Click += OnDiagnoseClick;
            _btnDiskMap.Click += OnDiskMapClick;
            _btnOpenLogs.Click += OnOpenLogsClick;
            _btnRestore.Click += OnRestoreClick;
            _btnCancel.Click += OnCancelClick;

            actions.Controls.Add(_btnOptimize);
            actions.Controls.Add(_btnDiagnose);
            actions.Controls.Add(_btnDiskMap);
            actions.Controls.Add(_btnOpenLogs);
            actions.Controls.Add(_btnRestore);
            actions.Controls.Add(_btnCancel);
            root.Controls.Add(actions, 0, 3);

            // --- 5. STATUS + PROGRESS ---
            Panel progPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 2, 0, 4) };
            _lblStatus = new Label
            {
                Text = "Pronto. Selecione as opcoes desejadas ou clique em 'OTIMIZAR E REPARAR AGORA'.",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                Dock = DockStyle.Top,
                Height = 18
            };
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 10,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100
            };
            progPanel.Controls.Add(_lblStatus);
            progPanel.Controls.Add(_progressBar);
            root.Controls.Add(progPanel, 0, 4);

            // --- 6. CONSOLE DE LOG AO VIVO ---
            _console = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = ConsoleBg,
                ForeColor = TextPrimary,
                Font = new Font("Consolas", 9.5F, FontStyle.Regular),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                WordWrap = true
            };
            root.Controls.Add(_console, 0, 5);

            AppendLogThreadSafe("PC Doctor Pro inicializado. Nenhum arquivo pessoal, jogo, senha ou recurso do Windows sera removido.", LogLevel.Info);
            AppendLogThreadSafe("Dica: Clique em 'Apenas Diagnosticar' para ver o laudo completo sem alterar nada, ou em 'OTIMIZAR E REPARAR AGORA'.", LogLevel.Header);
        }

        private Panel CreateStatCard(string title, out Label valueLbl, out Label subLbl)
        {
            Panel p = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardBg,
                Margin = new Padding(4, 2, 4, 2),
                Padding = new Padding(10, 8, 10, 6)
            };
            p.Paint += DrawCardBorder;

            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = TextMuted,
                Dock = DockStyle.Top,
                Height = 15
            };
            valueLbl = new Label
            {
                Text = "Carregando...",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Dock = DockStyle.Top,
                Height = 24
            };
            subLbl = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 7.8F, FontStyle.Regular),
                ForeColor = TextMuted,
                Dock = DockStyle.Fill
            };
            p.Controls.Add(subLbl);
            p.Controls.Add(valueLbl);
            p.Controls.Add(lblTitle);
            return p;
        }

        private Panel CreateModuleCard(string title, Color accent)
        {
            Panel p = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardBg,
                Margin = new Padding(4, 2, 4, 2),
                Padding = new Padding(10, 8, 10, 8)
            };
            p.Paint += DrawCardBorder;

            Panel bar = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = accent };
            Label hdr = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = accent,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft
            };
            p.Controls.Add(hdr);
            p.Controls.Add(bar);
            return p;
        }

        private FlowLayoutPanel CreateCheckFlow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 4, 0, 0)
            };
        }

        private CheckBox AddCheck(FlowLayoutPanel flow, string text, bool isChecked, ToolTip tip, string tooltipText)
        {
            CheckBox cb = new CheckBox
            {
                Text = text,
                Checked = isChecked,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                AutoSize = true,
                Margin = new Padding(2, 3, 2, 3),
                Cursor = Cursors.Hand
            };
            tip.SetToolTip(cb, tooltipText);
            flow.Controls.Add(cb);
            return cb;
        }

        private Button CreateButton(string text, Color bg, int width, bool bold)
        {
            Button btn = new Button
            {
                Text = text,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, bold ? FontStyle.Bold : FontStyle.Regular),
                Size = new Size(width, 36),
                Margin = new Padding(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void DrawCardBorder(object sender, PaintEventArgs e)
        {
            Panel p = sender as Panel;
            if (p == null) return;
            using (Pen pen = new Pen(CardBorder, 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnOptimize.Enabled = !busy;
            _btnDiagnose.Enabled = !busy;
            _btnDiskMap.Enabled = !busy;
            _btnRestore.Enabled = !busy;
            _btnCancel.Enabled = busy;
        }

        private void RefreshStatsAsync()
        {
            Task.Factory.StartNew(delegate
            {
                SystemStats st = OptimizerEngine.CollectQuickStats();
                if (IsDisposed) return;
                BeginInvoke((Action)delegate
                {
                    if (st.IsAdmin)
                    {
                        _lblAdminBadge.Text = "ADMIN ATIVO";
                        _lblAdminBadge.BackColor = AccentGreen;
                    }
                    else
                    {
                        _lblAdminBadge.Text = "SEM ADMIN";
                        _lblAdminBadge.BackColor = AccentRose;
                    }

                    _lblDiskValue.Text = string.Format("{0:N1} GB livres", st.DiskFreeGB);
                    _lblDiskSub.Text = string.Format("Total: {0:N0} GB ({1:N1}% usado)", st.DiskTotalGB, st.DiskUsedPct);
                    _lblDiskValue.ForeColor = st.DiskUsedPct >= 85 ? AccentRose : (st.DiskUsedPct >= 75 ? AccentAmber : AccentGreen);

                    _lblRamValue.Text = string.Format("{0:N1} / {1:N0} GB", st.RamFreeGB, st.RamTotalGB);
                    _lblRamSub.Text = "Memoria RAM livre / total";

                    _lblSsdValue.Text = st.SsdWarning ? "Atencao (Latencia)" : "Saudavel";
                    _lblSsdValue.ForeColor = st.SsdWarning ? AccentAmber : AccentGreen;
                    _lblSsdSub.Text = st.SsdSummary;

                    _lblOsValue.Text = st.OsName.Replace("Microsoft ", "");
                    _lblOsSub.Text = "Pronto para manutencao";
                });
            });
        }

        private void AppendLogThreadSafe(string message, LogLevel level)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)delegate { AppendLogThreadSafe(message, level); });
                return;
            }

            Color col = Color.FromArgb(203, 213, 225);
            if (level == LogLevel.Header) col = Color.FromArgb(56, 189, 248);
            else if (level == LogLevel.Success) col = Color.FromArgb(52, 211, 153);
            else if (level == LogLevel.Warning) col = Color.FromArgb(251, 191, 36);
            else if (level == LogLevel.Error) col = Color.FromArgb(251, 113, 133);

            _console.SelectionStart = _console.TextLength;
            _console.SelectionLength = 0;
            _console.SelectionColor = Color.FromArgb(100, 116, 139);
            _console.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] ");
            _console.SelectionColor = col;
            _console.AppendText(message + Environment.NewLine);
            _console.ScrollToCaret();
        }

        private void UpdateProgressThreadSafe(int percent, string status)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)delegate { UpdateProgressThreadSafe(percent, status); });
                return;
            }
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            _progressBar.Value = percent;
            _lblStatus.Text = status;
        }

        private OptimizationOptions ReadOptions()
        {
            return new OptimizationOptions
            {
                CleanTempAndLogs = _chkTempLogs.Checked,
                CleanOrphanUpdaters = _chkOrphanUpdaters.Checked,
                CleanDevAndShaderCaches = _chkDevShaders.Checked,
                CleanWindowsUpdateCache = _chkWinUpdateCache.Checked,
                CleanBrowserAndSpotifyCache = _chkBrowserSpotify.Checked,
                EmptyRecycleBin = _chkRecycleBin.Checked,

                RunSsdTrim = _chkTrim.Checked,
                OptimizeAhciAndPower = _chkAhciPower.Checked,
                FixStorAhciMsi = _chkStorAhciMsi.Checked,
                OptimizeNtfsForSsd = _chkNtfsSsd.Checked,
                DisableFastStartupAndP2P = _chkFastStartupP2P.Checked,
                LimitShadowStorage = _chkShadowLimit.Checked,

                CreateRestorePoint = _chkRestorePoint.Checked,
                RunHealthAudit = _chkHealthAudit.Checked,
                FixNetworkAndVBox = _chkNetworkVBox.Checked,
                RunChkdskOnline = _chkChkdsk.Checked,
                RunDismCleanup = _chkDismCleanup.Checked,
                RunSfcScan = _chkSfcScan.Checked,
                RunDismRestoreHealth = _chkDismRestore.Checked
            };
        }

        private void RunBackgroundTask(Action<CancellationToken> work)
        {
            if (_isBusy) return;
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;
            SetBusy(true);
            _progressBar.Value = 0;

            Task.Factory.StartNew(delegate
            {
                try
                {
                    work(token);
                }
                catch (Exception ex)
                {
                    AppendLogThreadSafe("Erro inesperado: " + ex.Message, LogLevel.Error);
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        BeginInvoke((Action)delegate
                        {
                            SetBusy(false);
                            RefreshStatsAsync();
                        });
                    }
                }
            });
        }

        private void OnOptimizeClick(object sender, EventArgs e)
        {
            OptimizationOptions opt = ReadOptions();
            RunBackgroundTask(delegate(CancellationToken ct)
            {
                _engine.RunOptimization(opt, ct);
            });
        }

        private void OnDiagnoseClick(object sender, EventArgs e)
        {
            RunBackgroundTask(delegate(CancellationToken ct)
            {
                _engine.RunDiagnosticOnly(ct);
            });
        }

        private void OnDiskMapClick(object sender, EventArgs e)
        {
            RunBackgroundTask(delegate(CancellationToken ct)
            {
                _engine.RunDiskSpaceMap(ct);
            });
        }

        private void OnOpenLogsClick(object sender, EventArgs e)
        {
            string logsDir = Path.GetDirectoryName(_engine.LogFilePath);
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
            Process.Start("explorer.exe", logsDir);
        }

        private void OnRestoreClick(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show(
                "Deseja restaurar as configuracoes de NTFS, Fast Startup, portas TCP/UDP e StorAHCI MSI para o padrao original do Windows?",
                "Restaurar Padroes Seguros",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (res != DialogResult.Yes) return;

            RunBackgroundTask(delegate(CancellationToken ct)
            {
                _engine.RestoreSafeDefaults(ct);
            });
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                AppendLogThreadSafe("Solicitando cancelamento seguro da etapa atual...", LogLevel.Warning);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0] == "--selftest")
            {
                SystemStats st = OptimizerEngine.CollectQuickStats();
                Console.WriteLine("SELFTEST_OK|Admin=" + st.IsAdmin + "|FreeGB=" + st.DiskFreeGB.ToString("N2"));
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
