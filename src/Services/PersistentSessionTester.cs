using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CMPADotNetTest.Models;

namespace CMPADotNetTest.Services
{
    public class PersistentSessionTester
    {
        private readonly StartupConfig _config;
        private readonly SessionPanelViewModel _vm;
        private CryptoContext _sessionCtx;

        public PersistentSessionTester(StartupConfig config, SessionPanelViewModel vm)
        {
            _config = config;
            _vm = vm;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var session = await OpenSessionWithRetryAsync(ct);
                if (session == null) break;

                bool sessionLost = false;
                try
                {
                    sessionLost = await RunOperationsAsync(session, ct);
                }
                finally
                {
                    string closingMsg = sessionLost
                        ? "Session lost — closing, will reconnect..."
                        : "Closing persistent session...";
                    _vm.SetStatus(SessionStatus.Initializing, closingMsg);
                    _vm.AddLog(string.Format("[{0}] {1}", Ts(), closingMsg));
                    _sessionCtx?.Dispose();
                    _sessionCtx = null;
                    ProtectAppService.CloseSession(session);
                }

                if (!sessionLost) break;

                _vm.IncrementReconnects();
                bool delayCancelled = false;
                try { await Task.Delay(1000, ct); }
                catch (OperationCanceledException) { delayCancelled = true; }
                if (delayCancelled) break;
            }

            _vm.SetStatus(SessionStatus.Completed, "Session closed");
            _vm.AddLog(string.Format("[{0}] Session closed.", Ts()));
        }

        private async Task<Ingrian.Security.Cryptography.NAESession> OpenSessionWithRetryAsync(CancellationToken ct)
        {
            _vm.SetStatus(SessionStatus.Initializing, "Opening persistent session...");

            while (!ct.IsCancellationRequested)
            {
                string openError = null;
                long openMs = 0;
                var sw = Stopwatch.StartNew();
                Ingrian.Security.Cryptography.NAESession session = null;
                try
                {
                    session = ProtectAppService.OpenSession(_config);
                    _sessionCtx = ProtectAppService.PrimeSession(session, _config.KeyName);
                    sw.Stop();
                    openMs = sw.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    openError = ex.Message;
                }

                if (openError == null)
                {
                    _vm.UpdateStats(_vm.SessionOpenStats, openMs);
                    _vm.AddLog(string.Format("[{0}] Session opened in {1} ms", Ts(), openMs));
                    _vm.SetStatus(SessionStatus.Running, "Persistent session active");
                    return session;
                }

                _vm.RecordError(_vm.SessionOpenStats);
                _vm.SetStatus(SessionStatus.Error, "Session open failed: " + openError);
                _vm.AddLog(string.Format("[{0}] ERROR opening session: {1} — retrying in 3s", Ts(), openError));

                bool cancelled = false;
                try { await Task.Delay(3000, ct); }
                catch (OperationCanceledException) { cancelled = true; }
                if (cancelled) return null;
            }

            return null;
        }

        private async Task<bool> RunOperationsAsync(
            Ingrian.Security.Cryptography.NAESession session, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // Encrypt
                string encrypted = null;
                string encError = null;
                long encMs = 0;
                var swEnc = Stopwatch.StartNew();
                try
                {
                    encrypted = ProtectAppService.Encrypt(_sessionCtx.Encryptor, _config.StaticTestValue);
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
                    _vm.AddLog(string.Format("[{0}] ENCRYPT ERROR (session lost): {1}", Ts(), encError));
                    return true;
                }
                _vm.UpdateStats(_vm.EncryptStats, encMs);

                // Decrypt
                string decError = null;
                long decMs = 0;
                var swDec = Stopwatch.StartNew();
                try
                {
                    ProtectAppService.Decrypt(_sessionCtx.Decryptor, encrypted);
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
                    _vm.AddLog(string.Format("[{0}] DECRYPT ERROR (session lost): {1}", Ts(), decError));
                    return true;
                }
                _vm.UpdateStats(_vm.DecryptStats, decMs);

                int iter = _vm.IncrementIteration();

                if (_config.LoopDelayMs >= 1000 || iter % 50 == 0)
                {
                    _vm.AddLog(string.Format("[{0}] iter {1} | enc {2} ms | dec {3} ms",
                        Ts(), iter, encMs, decMs));
                }

                // Configurable loop delay (None = run as fast as possible)
                if (_config.LoopDelayMs > 0)
                {
                    bool cancelled = false;
                    try { await Task.Delay(_config.LoopDelayMs, ct); }
                    catch (OperationCanceledException) { cancelled = true; }
                    if (cancelled || ct.IsCancellationRequested) break;
                }
                else if (ct.IsCancellationRequested)
                {
                    break;
                }
            }

            return false;
        }

        private static string Ts()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
