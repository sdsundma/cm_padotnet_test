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
                // Open session
                NAESession session = null;
                CryptoContext ctx = null;
                string openError = null;
                long openMs = 0;
                var swOpen = Stopwatch.StartNew();
                try
                {
                    session = ProtectAppService.OpenSession(_config);
                    ctx = ProtectAppService.PrimeSession(session, _config.KeyName);
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

                    // Brief pause before retry
                    bool cancelled = false;
                    try { await Task.Delay(1000, ct); }
                    catch (OperationCanceledException) { cancelled = true; }
                    if (cancelled) break;
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
                        encrypted = ProtectAppService.Encrypt(ctx.Encryptor, _config.StaticTestValue);
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
                    string decError = null;
                    long decMs = 0;
                    var swDec = Stopwatch.StartNew();
                    try
                    {
                        ProtectAppService.Decrypt(ctx.Decryptor, encrypted);
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

                    int iter = _vm.IncrementIteration();

                    if (_config.LoopDelayMs >= 1000 || iter % 50 == 0)
                    {
                        _vm.AddLog(string.Format("[{0}] iter {1} | open {2} ms | enc {3} ms | dec {4} ms",
                            Ts(), iter, openMs, encMs, decMs));
                    }
                }
                finally
                {
                    ctx?.Dispose();
                    ProtectAppService.CloseSession(session);
                    _vm.SetStatus(SessionStatus.Running, "Session closed — next cycle");
                }

                // Configurable loop delay (None = run as fast as possible)
                if (_config.LoopDelayMs > 0)
                {
                    bool cancelled = false;
                    try { await Task.Delay(_config.LoopDelayMs, ct); }
                    catch (OperationCanceledException) { cancelled = true; }
                    if (cancelled) break;
                }
            }

            _vm.SetStatus(SessionStatus.Completed, "Dynamic session loop complete");
            _vm.AddLog(string.Format("[{0}] Test stopped.", Ts()));
        }

        private static string Ts()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
