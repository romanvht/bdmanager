using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace bdmanager
{
    public class ProcessManager
    {
        private Process _byeDpiProcess;
        private Process _proxifyreProcess;
        private AppSettings _settings;

        public event EventHandler<string> LogMessage;
        public event EventHandler<bool> StatusChanged;

        public ProcessManager(AppSettings settings)
        {
            _settings = settings;
        }

        public bool IsRunning => _byeDpiProcess != null && !_byeDpiProcess.HasExited && _proxifyreProcess != null && !_proxifyreProcess.HasExited;

        public void Start()
        {
            try
            {
                if (!File.Exists(_settings.ByeDpiPath))
                {
                    RaiseLogMessage($"Файл ByeDPI не найден: {_settings.ByeDpiPath}");
                    return;
                }

                if (!File.Exists(_settings.ProxiFyrePath))
                {
                    RaiseLogMessage($"Файл ProxiFyre не найден: {_settings.ProxiFyrePath}");
                    return;
                }

                try
                {
                    _settings.UpdateProxiFyreConfig();
                    RaiseLogMessage("Конфигурация ProxiFyre обновлена");
                }
                catch (Exception configEx)
                {
                    RaiseLogMessage($"Ошибка при обновлении конфигурации ProxiFyre: {configEx.Message}");
                    return;
                }

                try
                {
                    _byeDpiProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = _settings.ByeDpiPath,
                            Arguments = _settings.GetByeDpiArguments(),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        },
                        EnableRaisingEvents = true
                    };

                    _byeDpiProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            RaiseLogMessage($"ByeDPI: {e.Data}");
                    };

                    _byeDpiProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            RaiseLogMessage($"ByeDPI: {e.Data}");
                    };

                    _byeDpiProcess.Exited += (sender, e) =>
                    {
                        Stop();
                    };

                    _byeDpiProcess.Start();
                    _byeDpiProcess.BeginOutputReadLine();
                    _byeDpiProcess.BeginErrorReadLine();

                    RaiseLogMessage("ByeDPI запущен");
                }
                catch (Exception byeDpiEx)
                {
                    RaiseLogMessage($"Ошибка при запуске ByeDPI: {byeDpiEx.Message}");
                    Stop();
                    return;
                }

                try
                {
                    _proxifyreProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = _settings.ProxiFyrePath,
                            WorkingDirectory = Path.GetDirectoryName(_settings.ProxiFyrePath),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        },
                        EnableRaisingEvents = true
                    };

                    _proxifyreProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            RaiseLogMessage($"ProxiFyre: {e.Data}");
                    };

                    _proxifyreProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            RaiseLogMessage($"ProxiFyre: {e.Data}");
                    };

                    _proxifyreProcess.Exited += (sender, e) =>
                    {
                        Stop();
                    };

                    _proxifyreProcess.Start();
                    _proxifyreProcess.BeginOutputReadLine();
                    _proxifyreProcess.BeginErrorReadLine();

                    RaiseLogMessage("ProxiFyre запущен");
                }
                catch (Exception proxiFyreEx)
                {
                    RaiseLogMessage($"Ошибка при запуске ProxiFyre: {proxiFyreEx.Message}");
                    Stop();
                    return;
                }

                RaiseStatusChanged(true);
            }
            catch (Exception ex)
            {
                RaiseLogMessage($"Общая ошибка при запуске: {ex.Message}");
                if (ex.InnerException != null)
                {
                    RaiseLogMessage($"Внутренняя ошибка: {ex.InnerException.Message}");
                }
                Stop();
            }
        }

        public void Stop()
        {
            try
            {
                if (_byeDpiProcess != null && !_byeDpiProcess.HasExited)
                {
                    try 
                    { 
                        _byeDpiProcess.Kill();
                        _byeDpiProcess = null;
                        RaiseLogMessage("ByeDPI процесс остановлен");
                    }
                    catch (Exception ex)
                    {
                        RaiseLogMessage($"Ошибка при остановке ByeDPI: {ex.Message}");
                    }
                }

                if (_proxifyreProcess != null && !_proxifyreProcess.HasExited)
                {
                    try 
                    { 
                        _proxifyreProcess.Kill();
                        _proxifyreProcess = null;
                        RaiseLogMessage("ProxiFyre процесс остановлен");
                    }
                    catch (Exception ex)
                    {
                        RaiseLogMessage($"Ошибка при остановке ProxiFyre: {ex.Message}");
                    }
                }

                RaiseStatusChanged(false);
            }
            catch (Exception ex)
            {
                RaiseLogMessage($"Общая ошибка при остановке процессов: {ex.Message}");
            }
        }

        private void RaiseLogMessage(string message)
        {
            LogMessage?.Invoke(this, message);
        }

        private void RaiseStatusChanged(bool isRunning)
        {
            StatusChanged?.Invoke(this, isRunning);
        }

        public void CleanupOnStartup()
        {
            try
            {
                bool processesFound = false;
                
                var byeDpiProcesses = Process.GetProcessesByName("ciadpi");
                if (byeDpiProcesses.Length > 0)
                {
                    processesFound = true;
                    foreach (var process in byeDpiProcesses)
                    {
                        try
                        {
                            process.Kill();
                            RaiseLogMessage("Остановлен запущенный процесс ByeDPI");
                        }
                        catch (Exception ex)
                        {
                            RaiseLogMessage($"Ошибка при остановке процесса ByeDPI: {ex.Message}");
                        }
                    }
                }
                
                var proxiFyreProcesses = Process.GetProcessesByName("ProxiFyre");
                if (proxiFyreProcesses.Length > 0)
                {
                    processesFound = true;
                    foreach (var process in proxiFyreProcesses)
                    {
                        try
                        {
                            process.Kill();
                            RaiseLogMessage("Остановлен запущенный процесс ProxiFyre");
                        }
                        catch (Exception ex)
                        {
                            RaiseLogMessage($"Ошибка при остановке процесса ProxiFyre: {ex.Message}");
                        }
                    }
                }
                
                if (processesFound)
                {
                    RaiseStatusChanged(false);
                }
            }
            catch (Exception ex)
            {
                RaiseLogMessage($"Ошибка при очистке процессов при запуске: {ex.Message}");
            }
        }
    }
} 