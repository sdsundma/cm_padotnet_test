using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ingrian.Security.Cryptography;
using CMPADotNetTest.Models;

namespace CMPADotNetTest.Services
{
    public class DynamicSessionTester
    {
        private readonly StartupConfig _config;
        private readonly SessionPanelViewModel _vm;
        private readonly Random _rng = new Random();

        public DynamicSessionTester(StartupConfig config, SessionPanelViewModel vm)
        {
            _config = config;
            _vm = vm;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            _vm.SetStatus(SessionStatus.Running, "Dynamic session loop active");

            while (!ct.IsCancellationRequested)
            {
                int delayMs = _rng.Next(100, 5001);
                bool cancelled = false;
                try { await Task.Delay(delayMs, ct); }
                catch (OperationCanceledException) { cancelled = true; }
                if (cancelled) break;

                if (ct.IsCancellationRequested) break;

                // Open session
                NAESession session = null;
                string openError = null;
                long openMs = 0;
                var swOpen = Stopwatch.StartNew();
                try
                {
                    session = ProtectAppService.OpenSession(_config);
                    ProtectAppService.PrimeSession(session, _config.KeyName);
                    swOpen.Stop();
                    openMs = swOpen.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    swOpen.Stop();
                    openError = ex.Message;
                }

                if (openError != null)
                {
                    _vm.RecordError(_vm.SessionOpenStats);
                    _vm.SetStatus(SessionStatus.Error, "Session open failed: " + openError);
                    _vm.AddLog(string.Format("[{0}] ERROR opening session: {1} — retrying next cycle", Ts(), openError));
                    continue;
                }

                _vm.UpdateStats(_vm.SessionOpenStats, openMs);
                _vm.SetStatus(SessionStatus.Running, "Session open");

                try
                {
                    // Encrypt
                    string encrypted = null;
                    string encError = null;
                    long encMs = 0;
                    var swEnc = Stopwatch.StartNew();
                    try
                    {
                        encrypted = ProtectAppService.Encrypt(session, _config.KeyName, _config.StaticTestValue);
                        swEnc.Stop();
                        encMs = swEnc.ElapsedMilliseconds;
                    }
                    catch (Exception ex)
                    {
                        swEnc.Stop();
                        encError = ex.Message;
                    }

                    if (encError != null)
                    {
                        _vm.RecordError(_vm.EncryptStats);
                        _vm.AddLog(string.Format("[{0}] ENCRYPT ERROR: {1}", Ts(), encError));
                        continue;
                    }
                    _vm.UpdateStats(_vm.EncryptStats, encMs);

                    // Decrypt
                    string decrypted = null;
                    string decError = null;
                    long decMs = 0;
                    var swDec = Stopwatch.StartNew();
                    try
                    {
                        decrypted = ProtectAppService.Decrypt(session, _config.KeyName, encrypted);
                        swDec.Stop();
                        decMs = swDec.ElapsedMilliseconds;
                    }
                    catch (Exception ex)
                    {
                        swDec.Stop();
                        decError = ex.Message;
                    }

                    if (decError != null)
                    {
                        _vm.RecordError(_vm.DecryptStats);
                        _vm.AddLog(string.Format("[{0}] DECRYPT ERROR: {1}", Ts(), decError));
                        continue;
                    }
                    _vm.UpdateStats(_vm.DecryptStats, decMs);

                    _vm.IncrementIteration();
                    _vm.AddLog(string.Format("[{0}] #{1}  Open={2}ms  Enc={3}ms  Dec={4}ms",
                        Ts(), _vm.Iteration, openMs, encMs, decMs));
                    _vm.AddLog(string.Format("         ENC: {0}", Truncate(encrypted, 48)));
                    _vm.AddLog(string.Format("         DEC: {0}", decrypted));
                }
                finally
                {
                    ProtectAppService.CloseSession(session);
                    _vm.SetStatus(SessionStatus.Running, "Session closed — waiting next cycle");
                }
            }

            _vm.SetStatus(SessionStatus.Completed, "Dynamic session loop complete");
        }

        private static string Ts()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }

        private static string Truncate(string s, int max)
        {
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }
    }
}
