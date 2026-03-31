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
        private readonly Random _rng = new Random();

        public PersistentSessionTester(StartupConfig config, SessionPanelViewModel vm)
        {
            _config = config;
            _vm = vm;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            // Outer loop: re-opens the session whenever a closure is detected.
            while (!ct.IsCancellationRequested)
            {
                var session = await OpenSessionWithRetryAsync(ct);
                if (session == null) break;     // cancelled during open

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
                    ProtectAppService.CloseSession(session);
                }

                if (!sessionLost) break;    // normal run-complete, exit

                // Brief pause before reconnect attempt
                _vm.IncrementReconnects();
                bool delayCancelled = false;
                try { await Task.Delay(1000, ct); }
                catch (OperationCanceledException) { delayCancelled = true; }
                if (delayCancelled) break;
            }

            _vm.SetStatus(SessionStatus.Completed, "Session closed");
        }

        // Opens a session with unlimited retries until success or cancellation.
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
                    ProtectAppService.PrimeSession(session, _config.KeyName);
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

        // Runs encrypt/decrypt loop on the given session.
        // Returns true if the session was lost (reconnect needed), false on normal cancellation.
        private async Task<bool> RunOperationsAsync(
            Ingrian.Security.Cryptography.NAESession session, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                int delayMs = _rng.Next(100, 5001);
                bool cancelled = false;
                try { await Task.Delay(delayMs, ct); }
                catch (OperationCanceledException) { cancelled = true; }
                if (cancelled || ct.IsCancellationRequested) break;

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
                    _vm.AddLog(string.Format("[{0}] ENCRYPT ERROR (session lost): {1}", Ts(), encError));
                    return true;    // session lost — trigger reconnect
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
                    _vm.AddLog(string.Format("[{0}] DECRYPT ERROR (session lost): {1}", Ts(), decError));
                    return true;    // session lost — trigger reconnect
                }
                _vm.UpdateStats(_vm.DecryptStats, decMs);

                _vm.IncrementIteration();
                _vm.AddLog(string.Format("[{0}] #{1}  Enc={2}ms  Dec={3}ms",
                    Ts(), _vm.Iteration, encMs, decMs));
                _vm.AddLog(string.Format("         ENC: {0}", Truncate(encrypted, 48)));
                _vm.AddLog(string.Format("         DEC: {0}", decrypted));
            }

            return false;   // normal cancellation / run complete
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
