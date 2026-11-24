using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace OtimizadorPC
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Header
    }

    public class SystemStats
    {
        public bool IsAdmin;
        public double DiskTotalGB;
        public double DiskFreeGB;
        public double DiskUsedPct;
        public double RamTotalGB;
        public double RamFreeGB;
        public string OsName;
        public string SsdSummary;
        public bool SsdWarning;
    }

    public class OptimizationOptions
    {
        // Limpeza segura
        public bool CleanTempAndLogs;
        public bool CleanOrphanUpdaters;
        public bool CleanDevAndShaderCaches;
        public bool CleanWindowsUpdateCache;
        public bool CleanBrowserAndSpotifyCache;
        public bool EmptyRecycleBin;

        // Anti-SSD 100% e Armazenamento
        public bool RunSsdTrim;
        public bool OptimizeAhciAndPower;
        public bool FixStorAhciMsi;
        public bool OptimizeNtfsForSsd;
        public bool DisableFastStartupAndP2P;
        public bool LimitShadowStorage;

        // Diagnostico e Reparo
        public bool CreateRestorePoint;
        public bool RunHealthAudit;
        public bool FixNetworkAndVBox;
        public bool RunChkdskOnline;
        public bool RunSfcScan;
        public bool RunDismCleanup;
        public bool RunDismRestoreHealth;
    }

    public class OptimizerEngine
    {
        private readonly Action<string, LogLevel> _log;
        private readonly Action<int, string> _progress;
        private readonly string _logFilePath;
        private readonly object _fileLock = new object();

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public string LogFilePath { get { return _logFilePath; } }

        public OptimizerEngine(Action<string, LogLevel> logCallback, Action<int, string> progressCallback)
        {
            _log = logCallback;
            _progress = progressCallback;
            string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            try { Directory.CreateDirectory(logsDir); } catch { }
            _logFilePath = Path.Combine(logsDir, "otimizador_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
        }

        private void Log(string msg, LogLevel level)
        {
            string line = string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), msg);
            lock (_fileLock)
            {
                try { File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8); } catch { }
            }
            if (_log != null) _log(msg, level);
        }

        private void ReportProgress(int pct, string status)
        {
            if (_progress != null) _progress(pct, status);
        }

        public static bool IsRunningAsAdmin()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static double GetDriveCFreeGB()
        {
            try
            {
                DriveInfo di = new DriveInfo("C");
                return di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            }
            catch { return 0; }
        }

        public static SystemStats CollectQuickStats()
        {
            SystemStats st = new SystemStats();
            st.IsAdmin = IsRunningAsAdmin();
            st.OsName = "Windows";
            st.SsdSummary = "Verificando...";

            try
            {
                DriveInfo di = new DriveInfo("C");
                st.DiskTotalGB = di.TotalSize / (1024.0 * 1024.0 * 1024.0);
                st.DiskFreeGB = di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                st.DiskUsedPct = st.DiskTotalGB > 0 ? ((st.DiskTotalGB - st.DiskFreeGB) / st.DiskTotalGB) * 100.0 : 0;
            }
            catch { }

            try
            {
                using (ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT Caption, BuildNumber, FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject mo in mos.Get())
                    {
                        st.OsName = string.Format("{0} (Build {1})", mo["Caption"], mo["BuildNumber"]);
                        double totalKB = Convert.ToDouble(mo["TotalVisibleMemorySize"]);
                        double freeKB = Convert.ToDouble(mo["FreePhysicalMemory"]);
                        st.RamTotalGB = totalKB / (1024.0 * 1024.0);
                        st.RamFreeGB = freeKB / (1024.0 * 1024.0);
                        break;
                    }
                }
            }
            catch { }

            try
            {
                string smartOut = RunPowerShellCapture(
                    "$dn = (Get-Partition -DriveLetter C -EA SilentlyContinue | Get-Disk -EA SilentlyContinue).Number; " +
                    "$d = if ($null -ne $dn) { Get-PhysicalDisk | Where-Object { $_.DeviceId -eq $dn.ToString() } | Select-Object -First 1 } else { Get-PhysicalDisk | Select-Object -First 1 }; " +
                    "$r = $d | Get-StorageReliabilityCounter -EA SilentlyContinue; " +
                    "if ($d) { Write-Output ($d.FriendlyName + '|' + $d.HealthStatus + '|' + $r.Temperature + '|' + $r.ReadLatencyMax + '|' + $r.WriteLatencyMax + '|' + $r.ReadErrorsUncorrected) }",
                    8000);
                if (!string.IsNullOrWhiteSpace(smartOut))
                {
                    string[] p = smartOut.Trim().Split('|');
                    if (p.Length >= 6)
                    {
                        string name = p[0].Trim();
                        string health = p[1].Trim();
                        string temp = string.IsNullOrEmpty(p[2]) ? "?" : p[2].Trim() + "C";
                        long rLat = 0, wLat = 0;
                        long.TryParse(p[3], out rLat);
                        long.TryParse(p[4], out wLat);
                        st.SsdSummary = string.Format("{0} ({1}, {2}, LatMax: {3}/{4}ms)", name, health, temp, rLat, wLat);
                        st.SsdWarning = (health != "Healthy" || rLat > 500 || wLat > 500);
                    }
                }
                else
                {
                    st.SsdSummary = "SMART indisponivel (rode como Admin)";
                }
            }
            catch
            {
                st.SsdSummary = "Disco C: pronto";
            }

            return st;
        }

        private static string RunPowerShellCapture(string script, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "\\\"") + "\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = Encoding.UTF8;

                using (Process proc = Process.Start(psi))
                {
                    StringBuilder sb = new StringBuilder();
                    proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) sb.AppendLine(e.Data);
                    };
                    proc.BeginOutputReadLine();
                    if (!proc.WaitForExit(timeoutMs))
                    {
                        try { proc.Kill(); } catch { }
                    }
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }

        private int RunCommandStreaming(string fileName, string arguments, CancellationToken ct, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = fileName;
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                using (Process proc = Process.Start(psi))
                {
                    proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            string clean = e.Data.Replace("\0", "").Trim();
                            if (!string.IsNullOrEmpty(clean) && !clean.StartsWith("[="))
                                Log("   " + clean, LogLevel.Info);
                        }
                    };
                    proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            string clean = e.Data.Replace("\0", "").Trim();
                            if (!string.IsNullOrEmpty(clean))
                                Log("   [Aviso] " + clean, LogLevel.Warning);
                        }
                    };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    int waited = 0;
                    while (!proc.WaitForExit(250))
                    {
                        waited += 250;
                        if (ct.IsCancellationRequested || (timeoutMs > 0 && waited >= timeoutMs))
                        {
                            try { proc.Kill(); } catch { }
                            return -1;
                        }
                    }
                    return proc.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Log("   Erro ao executar " + fileName + ": " + ex.Message, LogLevel.Error);
                return -1;
            }
        }

        private long CleanDirectoryContents(string path, CancellationToken ct, bool removeRoot)
        {
            long freedBytes = 0;
            if (!Directory.Exists(path)) return 0;

            Stack<string> stack = new Stack<string>();
            stack.Push(path);
            List<string> dirsToDelete = new List<string>();

            while (stack.Count > 0)
            {
                if (ct.IsCancellationRequested) break;
                string current = stack.Pop();
                try
                {
                    DirectoryInfo di = new DirectoryInfo(current);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                    foreach (FileInfo fi in di.EnumerateFiles())
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            long len = fi.Length;
                            if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                                fi.Attributes = FileAttributes.Normal;
                            fi.Delete();
                            freedBytes += len;
                        }
                        catch { /* arquivo em uso - pula com seguranca */ }
                    }

                    foreach (DirectoryInfo sub in di.EnumerateDirectories())
                    {
                        try
                        {
                            if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                            {
                                stack.Push(sub.FullName);
                                dirsToDelete.Add(sub.FullName);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Remove subpastas vazias de baixo para cima
            for (int i = dirsToDelete.Count - 1; i >= 0; i--)
            {
                try { Directory.Delete(dirsToDelete[i], false); } catch { }
            }
            if (removeRoot)
            {
                try { Directory.Delete(path, false); } catch { }
            }
            return freedBytes;
        }

        // =========================================================================
        // 1. DIAGNOSTICO COMPLETO (SOMENTE LEITURA)
        // =========================================================================
        public void RunDiagnosticOnly(CancellationToken ct)
        {
            Log("=== INICIANDO DIAGNOSTICO TECNICO COMPLETO (SOMENTE LEITURA) ===", LogLevel.Header);
            ReportProgress(10, "Lendo saude SMART do disco...");

            string psAudit =
                "$ErrorActionPreference = 'SilentlyContinue'; " +
                "Write-Output '[DISCO & SMART]'; " +
                "Get-PhysicalDisk | ForEach-Object { " +
                "  $r = $_ | Get-StorageReliabilityCounter; " +
                "  Write-Output ('  ' + $_.FriendlyName + ' | Bus:' + $_.BusType + ' | Health:' + $_.HealthStatus + ' | FW:' + $_.FirmwareVersion); " +
                "  if ($r) { Write-Output ('  Temp:' + $r.Temperature + 'C | Wear:' + $r.Wear + '% | Horas:' + $r.PowerOnHours + ' | ReadErr:' + $r.ReadErrorsTotal + ' | LatMax R/W/Flush: ' + $r.ReadLatencyMax + '/' + $r.WriteLatencyMax + '/' + $r.FlushLatencyMax + ' ms') } " +
                "}; " +
                "Write-Output '[CONTROLADORA AHCI / MSI]'; " +
                "Get-PnpDevice -Class HDC -Status OK | Where-Object { $_.InstanceId -like 'PCI\\*' } | ForEach-Object { " +
                "  $p = 'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\' + $_.InstanceId + '\\Device Parameters\\Interrupt Management\\MessageSignaledInterruptProperties'; " +
                "  $m = (Get-ItemProperty $p -Name MSISupported).MSISupported; " +
                "  Write-Output ('  ' + $_.FriendlyName + ' | MSISupported=' + $m) " +
                "}; " +
                "Write-Output '[CONFIGURACOES DE DISCO]'; " +
                "$trim = (& fsutil behavior query DisableDeleteNotify 2>&1 | Select-Object -First 1); Write-Output ('  TRIM: ' + $trim); " +
                "$la = (& fsutil behavior query disablelastaccess 2>&1 | Select-Object -First 1); Write-Output ('  LastAccess: ' + $la); " +
                "$fb = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power' -Name HiberbootEnabled).HiberbootEnabled; Write-Output ('  FastStartup(HiberbootEnabled): ' + $fb); " +
                "$pf = Get-CimInstance Win32_PageFileUsage; foreach ($x in $pf) { Write-Output ('  PageFile: ' + $x.Name + ' | Alocado: ' + $x.AllocatedBaseSize + ' MB | Pico: ' + $x.PeakUsage + ' MB') }; " +
                "$vb = Get-NetAdapterBinding -ComponentID '*vbox*' | Where-Object { $_.Enabled -eq $true }; " +
                "if ($vb) { Write-Output ('  [ALERTA] Filtro VBoxNetLwf ATIVO em ' + $vb.Count + ' adaptador(es) - risco de travamento/BSOD!') } else { Write-Output '  VBoxNetLwf: Desativado/OK' }; " +
                "$wd = (Get-Service WinDefend).Status; $wdStart = (Get-Service WinDefend).StartType; " +
                "Write-Output ('  Windows Defender: Status=' + $wd + ' | StartType=' + $wdStart); " +
                "Write-Output '[ERROS CRITICOS NO EVENT VIEWER (ULTIMOS 7 DIAS)]'; " +
                "$ev = Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=(Get-Date).AddDays(-7); Level=1,2}; " +
                "if ($ev) { " +
                "  $ev | Group-Object ProviderName, Id | Sort-Object Count -Descending | Select-Object -First 10 | ForEach-Object { " +
                "    Write-Output ('  [' + $_.Count + 'x] ' + $_.Group[0].ProviderName + ' | ID ' + $_.Group[0].Id + ' | Ultimo: ' + $_.Group[0].TimeCreated.ToString('dd/MM HH:mm')) " +
                "  } " +
                "} else { Write-Output '  Nenhum erro critico nos ultimos 7 dias!' }";

            ReportProgress(45, "Consultando Event Viewer, drivers e politicas...");
            string result = RunPowerShellCapture(psAudit, 30000);
            foreach (string rawLine in result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.TrimEnd();
                LogLevel lvl = LogLevel.Info;
                if (line.StartsWith("[")) lvl = LogLevel.Header;
                else if (line.Contains("[ALERTA]") || line.Contains("Kernel-Power") || line.Contains("VBoxNetLwf") || line.Contains("WindowsUpdateClient") || line.Contains("Volsnap")) lvl = LogLevel.Warning;
                else if (line.Contains("Healthy") || line.Contains("Desativado/OK") || line.Contains("Nenhum erro")) lvl = LogLevel.Success;
                Log(line, lvl);
            }

            ReportProgress(100, "Diagnostico concluido!");
            Log("=== DIAGNOSTICO FINALIZADO ===", LogLevel.Success);
        }

        // =========================================================================
        // 2. MAPA DE ESPACO EM DISCO (SINGLE-PASS RAPIDO EM C#)
        // =========================================================================
        public void RunDiskSpaceMap(CancellationToken ct)
        {
            Log("=== MAPEANDO MAIORES PASTAS E ARQUIVOS EM C:\\ (AGUARDE) ===", LogLevel.Header);
            ReportProgress(10, "Escaneando C:\\ em passada unica...");

            Stopwatch sw = Stopwatch.StartNew();
            Dictionary<string, long> dirSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            List<Tuple<long, string>> largeFiles = new List<Tuple<long, string>>();
            long fileCount = 0;

            WalkDisk("C:\\", 0, dirSizes, largeFiles, ref fileCount, ct);
            sw.Stop();

            if (ct.IsCancellationRequested)
            {
                Log("Mapeamento cancelado pelo usuario.", LogLevel.Warning);
                return;
            }

            Log(string.Format("Varredura concluida em {0:N1}s ({1:N0} arquivos lidos).", sw.Elapsed.TotalSeconds, fileCount), LogLevel.Success);

            ReportProgress(75, "Organizando maiores pastas...");
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            PrintTopChildren(dirSizes, "C:\\", 10);
            PrintTopChildren(dirSizes, userProfile, 12);
            PrintTopChildren(dirSizes, Path.Combine(userProfile, "AppData\\Local"), 12);
            PrintTopChildren(dirSizes, Path.Combine(userProfile, "AppData\\Roaming"), 8);

            Log("--- TOP 15 MAIORES ARQUIVOS INDIVIDUAIS (>= 500 MB) EM C:\\ ---", LogLevel.Header);
            foreach (Tuple<long, string> item in largeFiles.OrderByDescending(x => x.Item1).Take(15))
            {
                Log(string.Format("  {0,7:N2} GB | {1}", item.Item1 / (1024.0 * 1024.0 * 1024.0), item.Item2), LogLevel.Info);
            }

            ReportProgress(100, "Mapa de espaco concluido!");
        }

        private long WalkDisk(string dir, int depth, Dictionary<string, long> dirSizes, List<Tuple<long, string>> largeFiles, ref long fileCount, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return 0;
            long total = 0;
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0) return 0;

                foreach (FileInfo fi in di.EnumerateFiles())
                {
                    if (ct.IsCancellationRequested) return total;
                    try
                    {
                        long len = fi.Length;
                        total += len;
                        fileCount++;
                        if (len >= 500L * 1024 * 1024)
                            largeFiles.Add(Tuple.Create(len, fi.FullName));
                    }
                    catch { }
                }

                foreach (DirectoryInfo sub in di.EnumerateDirectories())
                {
                    if (ct.IsCancellationRequested) return total;
                    try
                    {
                        if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        total += WalkDisk(sub.FullName, depth + 1, dirSizes, largeFiles, ref fileCount, ct);
                    }
                    catch { }
                }
            }
            catch { }

            if (depth <= 5 && total >= 100L * 1024 * 1024)
                dirSizes[dir.TrimEnd('\\')] = total;

            return total;
        }

        private void PrintTopChildren(Dictionary<string, long> dirSizes, string parent, int topN)
        {
            string norm = parent.TrimEnd('\\');
            string prefix = norm + "\\";
            var items = dirSizes
                .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && kv.Key.IndexOf('\\', prefix.Length) == -1)
                .OrderByDescending(kv => kv.Value)
                .Take(topN)
                .ToList();

            if (items.Count == 0) return;
            Log("--- Top pastas em " + parent + " ---", LogLevel.Header);
            foreach (var kv in items)
            {
                Log(string.Format("  {0,7:N2} GB | {1}", kv.Value / (1024.0 * 1024.0 * 1024.0), kv.Key), LogLevel.Info);
            }
        }

        // =========================================================================
        // 3. EXECUCAO DA OTIMIZACAO COMPLETA SEGURA
        // =========================================================================
        public double RunOptimization(OptimizationOptions opt, CancellationToken ct)
        {
            double freeBefore = GetDriveCFreeGB();
            Log("=================================================================", LogLevel.Header);
            Log(string.Format("INICIANDO OTIMIZACAO SEGURA | Livre antes: {0:N2} GB", freeBefore), LogLevel.Header);
            Log("=================================================================", LogLevel.Header);

            List<Tuple<string, Action>> tasks = new List<Tuple<string, Action>>();
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (opt.CreateRestorePoint)
                tasks.Add(Tuple.Create<string, Action>("Criando Ponto de Restauracao do Windows (Backup)", delegate
                {
                    Log("[BACKUP] Criando Ponto de Restauracao em C:\\ antes das alteracoes...", LogLevel.Header);
                    string desc = "PC Doctor Pro - Backup " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    string ps =
                        "$ErrorActionPreference = 'Stop'; " +
                        "try { " +
                        "  Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue; " +
                        "  $srPath = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'; " +
                        "  if (Test-Path $srPath) { Set-ItemProperty -Path $srPath -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -ErrorAction SilentlyContinue }; " +
                        "  $vss = (& vssadmin list shadowstorage /for=C: 2>&1 | Out-String); " +
                        "  if ($vss -match 'Nenhum item|No items') { & vssadmin add shadowstorage /for=C: /on=C: /maxsize=4GB 2>&1 | Out-Null }; " +
                        "  Checkpoint-Computer -Description '" + desc + "' -RestorePointType 'MODIFY_SETTINGS'; " +
                        "  Write-Output 'RESTORE_OK:" + desc + "' " +
                        "} catch { Write-Output ('RESTORE_ERR:' + $_.Exception.Message) }";
                    string res = RunPowerShellCapture(ps, 90000).Trim();
                    if (res.Contains("RESTORE_OK:"))
                    {
                        Log("  Ponto de Restauracao criado com sucesso: \"" + desc + "\"", LogLevel.Success);
                    }
                    else
                    {
                        Log("  Aviso ao criar Ponto de Restauracao: " + res.Replace("RESTORE_ERR:", "").Trim(), LogLevel.Warning);
                        Log("  (Verifique se o servico 'Copias de Sombra de Volume' / Protecao do Sistema esta habilitado.)", LogLevel.Warning);
                    }
                }));

            if (opt.RunHealthAudit)
                tasks.Add(Tuple.Create<string, Action>("Auditoria de saude SMART e Event Viewer", delegate { RunDiagnosticOnly(ct); }));

            if (opt.CleanTempAndLogs)
                tasks.Add(Tuple.Create<string, Action>("Limpando arquivos temporarios e logs antigos", delegate
                {
                    Log("[LIMPEZA] Temporarios do Usuario, Windows\\Temp, CrashDumps, WER e CBS...", LogLevel.Header);
                    string[] dirs = new[]
                    {
                        Path.GetTempPath(),
                        @"C:\Windows\Temp",
                        Path.Combine(localAppData, "CrashDumps"),
                        @"C:\ProgramData\Microsoft\Windows\WER\ReportArchive",
                        @"C:\ProgramData\Microsoft\Windows\WER\ReportQueue",
                        @"C:\Windows\Logs\CBS"
                    };
                    long total = 0;
                    foreach (string d in dirs)
                    {
                        long b = CleanDirectoryContents(d, ct, false);
                        total += b;
                        if (b > 1024 * 1024) Log(string.Format("  Limpo {0}: +{1:N1} MB", d, b / (1024.0 * 1024.0)), LogLevel.Success);
                    }
                    Log(string.Format("  Subtotal temporarios: +{0:N2} GB", total / (1024.0 * 1024.0 * 1024.0)), LogLevel.Success);
                }));

            if (opt.CleanOrphanUpdaters)
                tasks.Add(Tuple.Create<string, Action>("Limpando instaladores orfaos (*-updater, NVIDIA, WinREAgent)", delegate
                {
                    Log("[LIMPEZA] Instaladores orfaos de atualizacoes (Electron/NVIDIA/WinRE)...", LogLevel.Header);
                    long total = 0;
                    total += CleanDirectoryContents(@"C:\ProgramData\NVIDIA Corporation\Downloader", ct, false);
                    total += CleanDirectoryContents(@"C:\$WinREAgent\Scratch", ct, false);
                    try
                    {
                        foreach (string updDir in Directory.GetDirectories(localAppData, "*-updater"))
                        {
                            long b = CleanDirectoryContents(updDir, ct, false);
                            total += b;
                            if (b > 5 * 1024 * 1024)
                                Log(string.Format("  Limpo {0}: +{1:N1} MB", Path.GetFileName(updDir), b / (1024.0 * 1024.0)), LogLevel.Success);
                        }
                    }
                    catch { }
                    Log(string.Format("  Subtotal instaladores orfaos: +{0:N2} GB", total / (1024.0 * 1024.0 * 1024.0)), LogLevel.Success);
                }));

            if (opt.CleanDevAndShaderCaches)
                tasks.Add(Tuple.Create<string, Action>("Limpando caches de dev e shaders (recriaveis)", delegate
                {
                    Log("[LIMPEZA] Caches recriaveis (uv, bun, pip, npm, DirectX/NVIDIA shaders)...", LogLevel.Header);
                    string[] dirs = new[]
                    {
                        Path.Combine(localAppData, @"uv\cache"),
                        Path.Combine(userProfile, @".bun\install\cache"),
                        Path.Combine(localAppData, @"pip\cache"),
                        Path.Combine(localAppData, "npm-cache"),
                        Path.Combine(localAppData, @"Yarn\Cache"),
                        Path.Combine(localAppData, "D3DSCache"),
                        Path.Combine(localAppData, @"NVIDIA\DXCache"),
                        Path.Combine(localAppData, @"NVIDIA\GLCache"),
                        Path.Combine(localAppData, @"AMD\DxCache"),
                        Path.Combine(localAppData, @"Intel\ShaderCache")
                    };
                    long total = 0;
                    foreach (string d in dirs)
                    {
                        long b = CleanDirectoryContents(d, ct, false);
                        total += b;
                        if (b > 5 * 1024 * 1024) Log(string.Format("  Limpo {0}: +{1:N1} MB", d, b / (1024.0 * 1024.0)), LogLevel.Success);
                    }
                    // pnpm store prune seguro (nao quebra hardlinks)
                    RunPowerShellCapture("if (Get-Command pnpm -EA SilentlyContinue) { pnpm store prune }", 30000);
                    Log(string.Format("  Subtotal caches dev/shader: +{0:N2} GB", total / (1024.0 * 1024.0 * 1024.0)), LogLevel.Success);
                }));

            if (opt.CleanWindowsUpdateCache)
                tasks.Add(Tuple.Create<string, Action>("Limpando cache do Windows Update e Delivery Optimization", delegate
                {
                    Log("[LIMPEZA] Reset seguro do cache do Windows Update...", LogLevel.Header);
                    RunPowerShellCapture("Stop-Service wuauserv, bits, DoSvc -Force -EA SilentlyContinue", 15000);
                    long total = 0;
                    total += CleanDirectoryContents(@"C:\Windows\SoftwareDistribution\Download", ct, false);
                    total += CleanDirectoryContents(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache", ct, false);
                    RunPowerShellCapture("Start-Service bits, wuauserv -EA SilentlyContinue", 15000);
                    Log(string.Format("  Cache Windows Update limpo: +{0:N2} GB", total / (1024.0 * 1024.0 * 1024.0)), LogLevel.Success);
                }));

            if (opt.CleanBrowserAndSpotifyCache)
                tasks.Add(Tuple.Create<string, Action>("Limpando cache de navegadores e Spotify (sem perder logins)", delegate
                {
                    Log("[LIMPEZA] Cache de navegadores (Cache/Code Cache) e Spotify Storage...", LogLevel.Header);
                    string[] dirs = new[]
                    {
                        Path.Combine(localAppData, @"Spotify\Storage"),
                        Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"),
                        Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Code Cache"),
                        Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"),
                        Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache"),
                        Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"),
                        Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Code Cache")
                    };
                    long total = 0;
                    foreach (string d in dirs)
                    {
                        long b = CleanDirectoryContents(d, ct, false);
                        total += b;
                        if (b > 5 * 1024 * 1024) Log(string.Format("  Limpo {0}: +{1:N1} MB", d, b / (1024.0 * 1024.0)), LogLevel.Success);
                    }
                    Log(string.Format("  Subtotal navegadores/Spotify: +{0:N2} GB (logins e favoritos intactos)", total / (1024.0 * 1024.0 * 1024.0)), LogLevel.Success);
                }));

            if (opt.EmptyRecycleBin)
                tasks.Add(Tuple.Create<string, Action>("Esvaziando Lixeira do Windows", delegate
                {
                    Log("[LIMPEZA] Esvaziando Lixeira...", LogLevel.Header);
                    // SHERB_NOCONFIRMATION (1) | SHERB_NOPROGRESSUI (2) | SHERB_NOSOUND (4) = 7
                    SHEmptyRecycleBin(IntPtr.Zero, null, 7);
                    Log("  Lixeira esvaziada com sucesso.", LogLevel.Success);
                }));

            if (opt.OptimizeAhciAndPower)
                tasks.Add(Tuple.Create<string, Action>("Desativando economia de energia AHCI/SATA LPM e PCIe ASPM", delegate
                {
                    Log("[SSD 100%] Ajustando energia SATA AHCI (HIPM/DIPM = Active) e PCIe...", LogLevel.Header);
                    string ps =
                        "powercfg -setacvalueindex SCHEME_CURRENT 0012ee47-9041-4b5d-9b77-535fba8b1442 0b2d69d7-a2a1-449c-9680-f91c70521c60 0; " +
                        "powercfg -setdcvalueindex SCHEME_CURRENT 0012ee47-9041-4b5d-9b77-535fba8b1442 0b2d69d7-a2a1-449c-9680-f91c70521c60 0; " +
                        "powercfg -setacvalueindex SCHEME_CURRENT 0012ee47-9041-4b5d-9b77-535fba8b1442 dab60367-53fe-4fbc-825e-521d069d2456 0; " +
                        "powercfg -setdcvalueindex SCHEME_CURRENT 0012ee47-9041-4b5d-9b77-535fba8b1442 dab60367-53fe-4fbc-825e-521d069d2456 0; " +
                        "powercfg -setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0; " +
                        "powercfg -setdcvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0; " +
                        "powercfg -setacvalueindex SCHEME_CURRENT 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0; " +
                        "powercfg -setactive SCHEME_CURRENT";
                    RunPowerShellCapture(ps, 15000);
                    Log("  AHCI Link Power Management = Active (0) e PCIe ASPM = Off.", LogLevel.Success);
                }));

            if (opt.FixStorAhciMsi)
                tasks.Add(Tuple.Create<string, Action>("Verificando bug StorAHCI MSI-Mode (anti-congelamento)", delegate
                {
                    Log("[SSD 100%] Verificando controladora AHCI (Fix oficial Microsoft StorAHCI)...", LogLevel.Header);
                    string ps =
                        "Get-PnpDevice -Class HDC -Status OK -EA SilentlyContinue | Where-Object { $_.InstanceId -like 'PCI\\*' -and $_.FriendlyName -like '*AHCI*' } | ForEach-Object { " +
                        "  $p = 'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\' + $_.InstanceId + '\\Device Parameters\\Interrupt Management\\MessageSignaledInterruptProperties'; " +
                        "  if (Test-Path $p) { " +
                        "    $v = (Get-ItemProperty $p -Name MSISupported -EA SilentlyContinue).MSISupported; " +
                        "    if ($v -eq 1) { Set-ItemProperty $p -Name MSISupported -Value 0 -Type DWord; Write-Output ('CORRIGIDO: ' + $_.FriendlyName + ' (MSISupported 1 -> 0)') } " +
                        "    else { Write-Output ('JA OTIMIZADO: ' + $_.FriendlyName + ' (MSISupported=' + $v + ')') } " +
                        "  } " +
                        "}";
                    string o = RunPowerShellCapture(ps, 15000);
                    Log(string.IsNullOrWhiteSpace(o) ? "  Nenhuma controladora StorAHCI afetada encontrada." : "  " + o.Trim(), LogLevel.Success);
                }));

            if (opt.OptimizeNtfsForSsd)
                tasks.Add(Tuple.Create<string, Action>("Otimizando sistema de arquivos NTFS (menos escritas no SSD)", delegate
                {
                    Log("[SSD 100%] Otimizando NTFS (DisableLastAccess, Disable8dot3, Cache RAM)...", LogLevel.Header);
                    RunCommandStreaming("fsutil", "behavior set disablelastaccess 1", ct, 10000);
                    RunCommandStreaming("fsutil", "behavior set disable8dot3 1", ct, 10000);
                    RunCommandStreaming("fsutil", "behavior set DisableDeleteNotify 0", ct, 10000);
                    SystemStats st = CollectQuickStats();
                    if (st.RamTotalGB >= 11.5)
                    {
                        RunCommandStreaming("fsutil", "behavior set memoryusage 2", ct, 10000);
                        Log("  RAM >= 12 GB detectada: NTFS memoryusage=2 ativado (mais cache na RAM).", LogLevel.Success);
                    }
                    Log("  TRIM garantido ativo (DisableDeleteNotify=0).", LogLevel.Success);
                }));

            if (opt.DisableFastStartupAndP2P)
                tasks.Add(Tuple.Create<string, Action>("Desativando Fast Startup e Delivery Optimization P2P", delegate
                {
                    Log("[ESTABILIDADE] Desativando Fast Startup (evita estado corrompido) e P2P Update...", LogLevel.Header);
                    try
                    {
                        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true))
                        {
                            if (k != null) k.SetValue("HiberbootEnabled", 0, RegistryValueKind.DWord);
                        }
                        using (RegistryKey k2 = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization"))
                        {
                            if (k2 != null) k2.SetValue("DODownloadMode", 0, RegistryValueKind.DWord);
                        }
                        Log("  Fast Startup desativado e Delivery Optimization P2P desligado.", LogLevel.Success);
                    }
                    catch (Exception ex)
                    {
                        Log("  Aviso ao ajustar registro: " + ex.Message, LogLevel.Warning);
                    }
                }));

            if (opt.LimitShadowStorage)
                tasks.Add(Tuple.Create<string, Action>("Ajustando limite de Shadow Storage (Volsnap) para 4 GB", delegate
                {
                    Log("[ESTABILIDADE] Ajustando Shadow Storage em C:\\ para 4 GB (evita erros Volsnap)...", LogLevel.Header);
                    string o = RunPowerShellCapture(
                        "$r = (& vssadmin resize shadowstorage /for=C: /on=C: /maxsize=4GB 2>&1 | Out-String); " +
                        "if ($r -match 'erro|error|nao foi|not found') { $r = (& vssadmin add shadowstorage /for=C: /on=C: /maxsize=4GB 2>&1 | Out-String) }; " +
                        "Write-Output $r.Trim()", 15000);
                    Log("  " + o.Trim().Replace("\r\n", " "), LogLevel.Success);
                }));

            if (opt.FixNetworkAndVBox)
                tasks.Add(Tuple.Create<string, Action>("Otimizando rede (DNS, portas TCP/UDP, filtro VBoxNetLwf)", delegate
                {
                    Log("[REDE & DRIVERS] Limpando DNS, expandindo portas efemeras e checando VBoxNetLwf...", LogLevel.Header);
                    RunCommandStreaming("ipconfig", "/flushdns", ct, 10000);
                    RunCommandStreaming("netsh", "int ipv4 set dynamicport tcp start=10000 num=55000", ct, 10000);
                    RunCommandStreaming("netsh", "int ipv4 set dynamicport udp start=10000 num=55000", ct, 10000);
                    try
                    {
                        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", true))
                        {
                            if (k != null) k.SetValue("TcpTimedWaitDelay", 30, RegistryValueKind.DWord);
                        }
                    }
                    catch { }
                    string vb = RunPowerShellCapture(
                        "Get-NetAdapterBinding -ComponentID '*vbox*' -EA SilentlyContinue | Where-Object { $_.Enabled } | ForEach-Object { " +
                        "  Disable-NetAdapterBinding -Name $_.Name -ComponentID $_.ComponentID -EA SilentlyContinue; " +
                        "  Write-Output ('Desativado VBoxNetLwf em ' + $_.Name) " +
                        "}", 15000);
                    if (!string.IsNullOrWhiteSpace(vb)) Log("  " + vb.Trim(), LogLevel.Success);
                    else Log("  Filtro VBoxNetLwf ja estava desativado/OK.", LogLevel.Success);
                }));

            if (opt.RunChkdskOnline)
                tasks.Add(Tuple.Create<string, Action>("Verificando erros no sistema de arquivos (chkdsk C: /scan)", delegate
                {
                    Log("[REPARO] Executando verificacao online de disco (chkdsk C: /scan)...", LogLevel.Header);
                    RunCommandStreaming("chkdsk", "C: /scan", ct, 180000);
                }));

            if (opt.RunDismRestoreHealth)
                tasks.Add(Tuple.Create<string, Action>("Reparando imagem do Windows (DISM /RestoreHealth)", delegate
                {
                    Log("[REPARO PROFUNDO] Executando DISM /Online /Cleanup-Image /RestoreHealth (pode levar varios minutos)...", LogLevel.Header);
                    RunCommandStreaming("dism.exe", "/Online /Cleanup-Image /RestoreHealth", ct, 1200000);
                }));

            if (opt.RunDismCleanup)
                tasks.Add(Tuple.Create<string, Action>("Compactando pasta WinSxS (DISM /StartComponentCleanup)", delegate
                {
                    Log("[REPARO & ESPACO] Limpando componentes antigos do Windows (WinSxS)...", LogLevel.Header);
                    RunCommandStreaming("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", ct, 600000);
                }));

            if (opt.RunSfcScan)
                tasks.Add(Tuple.Create<string, Action>("Verificando e reparando arquivos do Windows (SFC /scannow)", delegate
                {
                    Log("[REPARO] Executando SFC /scannow (integridade do sistema)...", LogLevel.Header);
                    RunCommandStreaming("sfc.exe", "/scannow", ct, 600000);
                }));

            if (opt.RunSsdTrim)
                tasks.Add(Tuple.Create<string, Action>("Executando TRIM no SSD (Optimize-Volume -ReTrim)", delegate
                {
                    Log("[SSD 100%] Enviando comando TRIM para recuperar velocidade de escrita do SSD...", LogLevel.Header);
                    string trimOut = RunPowerShellCapture("Optimize-Volume -DriveLetter C -ReTrim -Verbose 4>&1 | Out-String", 60000);
                    Log(string.IsNullOrWhiteSpace(trimOut) ? "  ReTrim executado em C:." : "  " + trimOut.Trim().Replace("\r\n", " "), LogLevel.Success);
                }));

            if (tasks.Count == 0)
            {
                Log("Nenhuma tarefa foi marcada!", LogLevel.Warning);
                return 0;
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    Log("Operacao cancelada pelo usuario.", LogLevel.Warning);
                    break;
                }
                int pct = (int)((i * 100.0) / tasks.Count);
                ReportProgress(pct, string.Format("({0}/{1}) {2}...", i + 1, tasks.Count, tasks[i].Item1));
                try
                {
                    tasks[i].Item2();
                }
                catch (Exception ex)
                {
                    Log("Erro na etapa '" + tasks[i].Item1 + "': " + ex.Message, LogLevel.Error);
                }
            }

            double freeAfter = GetDriveCFreeGB();
            double diff = freeAfter - freeBefore;
            ReportProgress(100, "Concluido!");
            Log("=================================================================", LogLevel.Header);
            Log(string.Format("CONCLUIDO! Livre antes: {0:N2} GB | Livre agora: {1:N2} GB | Saldo: {2}{3:N2} GB",
                freeBefore, freeAfter, (diff >= 0 ? "+" : ""), diff), LogLevel.Success);
            Log("Log salvo em: " + _logFilePath, LogLevel.Info);
            Log("DICA: Reinicie o PC se houve mudancas de driver/NTFS/SFC para tudo entrar em vigor.", LogLevel.Warning);
            Log("=================================================================", LogLevel.Header);
            return diff;
        }

        // =========================================================================
        // 4. RESTAURAR PADROES SEGUROS DO WINDOWS (ROLLBACK TOTAL)
        // =========================================================================
        public void RestoreSafeDefaults(CancellationToken ct)
        {
            Log("=== RESTAURANDO CONFIGURACOES PADRAO DO WINDOWS ===", LogLevel.Header);
            ReportProgress(20, "Restaurando NTFS e Fast Startup...");
            RunCommandStreaming("fsutil", "behavior set disablelastaccess 2", ct, 10000);
            RunCommandStreaming("fsutil", "behavior set disable8dot3 2", ct, 10000);
            RunCommandStreaming("fsutil", "behavior set memoryusage 1", ct, 10000);

            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true))
                {
                    if (k != null) k.SetValue("HiberbootEnabled", 1, RegistryValueKind.DWord);
                }
                using (RegistryKey k2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", true))
                {
                    if (k2 != null) k2.DeleteValue("DODownloadMode", false);
                }
                using (RegistryKey k3 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", true))
                {
                    if (k3 != null) k3.DeleteValue("TcpTimedWaitDelay", false);
                }
            }
            catch { }

            ReportProgress(60, "Restaurando portas de rede e StorAHCI...");
            RunCommandStreaming("netsh", "int ipv4 set dynamicport tcp start=49152 num=16384", ct, 10000);
            RunCommandStreaming("netsh", "int ipv4 set dynamicport udp start=49152 num=16384", ct, 10000);

            RunPowerShellCapture(
                "Get-PnpDevice -Class HDC -Status OK -EA SilentlyContinue | Where-Object { $_.InstanceId -like 'PCI\\*' -and $_.FriendlyName -like '*AHCI*' } | ForEach-Object { " +
                "  $p = 'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\' + $_.InstanceId + '\\Device Parameters\\Interrupt Management\\MessageSignaledInterruptProperties'; " +
                "  if (Test-Path $p) { Set-ItemProperty $p -Name MSISupported -Value 1 -Type DWord -EA SilentlyContinue } " +
                "}", 15000);

            ReportProgress(100, "Padroes restaurados!");
            Log("Configuracoes restauradas para o padrao do Windows (reinicie para aplicar).", LogLevel.Success);
        }
    }
}
