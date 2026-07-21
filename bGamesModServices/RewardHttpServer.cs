using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;

namespace bGamesMod.bGamesModServices
{
    // Resultado que el mod devuelve tras intentar aplicar una recompensa en el hilo principal.
    class RewardApplicationResult
    {
        public bool Succeeded { get; }
        public string? ErrorCode { get; }
        public string? Summary { get; }

        public RewardApplicationResult(bool succeeded, string? errorCode = null, string? summary = null)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            Summary = summary;
        }
    }

    // Método del mod que aplica una recompensa y devolver el resultado.
    delegate RewardApplicationResult ApplyRewardHandler(int rewardId);

    /// <summary>
    /// Servidor HTTP liviano que corre en segundo plano y recibe canjes de recompensas.
    /// Expone POST /reward con un JSON que incluye reward_id.
    /// Valida y encola los canjes para que el mod los aplique en el hilo principal.
    /// </summary>
    internal static class RewardHttpServer
    {
        private const int Port = 7878;

        // Listener HTTP usado por ListenLoop().
        private static HttpListener? _listener;

        // Hilo que escucha solicitudes HTTP. Se inicia en Start() y se detiene en Stop().
        private static Thread? _thread;

        // Callback del mod para aplicar una recompensa desde el hilo principal.
        private static ApplyRewardHandler? _onReward;

        // Logger compartido por los hilos HTTP y principal.
        private static Action<string>? _logger;

        // Cola segura para transferir canjes del hilo HTTP al hilo principal del juego.
        private static readonly ConcurrentQueue<PendingReward> PendingRewards = new ConcurrentQueue<PendingReward>();

        // Tiempo máximo para que el hilo principal procese un canje ya encolado.
        private static readonly TimeSpan RewardApplicationTimeout = TimeSpan.FromSeconds(5);

        // Indica si el juego está listo para aplicar recompensas, volatile sincroniza su visibilidad entre ambos hilos.
        private static volatile bool _gameReady;

        // Inicia el listener HTTP en segundo plano.
        public static void Start(ApplyRewardHandler onReward, Action<string>? logger = null)
        {
            if (_listener != null)
                return;

            _onReward = onReward ?? throw new ArgumentNullException(nameof(onReward));
            _logger = logger ?? Console.WriteLine;

            // Limpia cualquier canje pendiente de una ejecución anterior.
            PendingRewards.Clear();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();

            // Escucha solicitudes sin bloquear el hilo principal del juego.
            _thread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "bGamesMod-RewardHttpServer"
            };
            _thread.Start();

            Log($"[RewardHttpServer] Listening on http://localhost:{Port}/");
        }

        public static void Stop()
        {
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // Ignora errores al detener el listener.
            }
            finally
            {
                _listener = null;
                _thread = null;
                _onReward = null;
                _logger = null;
                // Limpia el estado compartido para que un reinicio comience desde cero.
                PendingRewards.Clear();
                _gameReady = false;
            }
        }

        // Aplica en el hilo principal los canjes recibidos por HTTP.
        // Debe llamarse en cada tick del juego.
        public static void DrainPending()
        {
            // El método del mod se ejecuta aquí, donde es seguro modificar el estado del juego.
            while (PendingRewards.TryDequeue(out PendingReward? dequeuedReward))
            {
                PendingReward pendingReward = dequeuedReward!;

                try
                {
                    RewardApplicationResult result = _onReward!(pendingReward.RewardId);

                    // Entrega el resultado al request HTTP que originó este canje.
                    pendingReward.Completion.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    Log("[RewardHttpServer] Reward application error: " + ex.Message);
                    pendingReward.Completion.TrySetResult(new RewardApplicationResult(false,
                        "reward_application_failed",
                        "Failed to apply reward."));
                }
            }
        }

        // Pensado para cuando el juego vuelve al título y habian rewards pendientes. Cancela todos los canjes pendientes y devuelve un error a cada request HTTP.
        public static void DrainPendingAndCancel()
        {
            while (PendingRewards.TryDequeue(out PendingReward? pendingReward))
            {
                pendingReward.Completion.TrySetResult(new RewardApplicationResult(false,
                    "game_not_ready",
                    "The game is not ready to apply rewards."));
            }
        }

        // Setea si el juego está listo para aplicar recompensas.
        public static void SetGameReady(bool isReady)
        {
            _gameReady = isReady;
        }

        // Espera solicitudes HTTP en segundo plano.
        private static void ListenLoop()
        {
            while (true)
            {
                HttpListenerContext ctx;
                try
                {
                    // Bloquea hasta que llegue una solicitud.
                    if (_listener is null)
                        break;

                    ctx = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    // Ocurre al cerrar el listener mientras espera una solicitud.
                    break;
                }
                catch (Exception ex)
                {
                    Log("[RewardHttpServer] Listener error: " + ex.Message);
                    break;
                }

                // Cada request puede esperar al próximo tick; atenderlo aparte mantiene disponible /health.
                ThreadPool.QueueUserWorkItem(_ => HandleContext(ctx));
            }
        }

        // Aborda cada solicitud y decide si corresponde a /reward, /health o una respuesta 404.
        private static void HandleContext(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            try
            {
                // Solo POST /reward entra al flujo de recompensas.
                bool isReward = req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
                                && req.Url is not null
                                && req.Url.AbsolutePath.TrimEnd('/')
                                    .Equals("/reward", StringComparison.OrdinalIgnoreCase);

                bool isHealth = req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase)
                                && req.Url is not null
                                && req.Url.AbsolutePath.TrimEnd('/')
                                    .Equals("/health", StringComparison.OrdinalIgnoreCase);

                if (isReward)
                {
                    HandleReward(req, resp);
                }
                else if (isHealth)
                {
                    string gameReady = _gameReady ? "true" : "false";
                    SendJson(resp, 200,
                        $"{{\"status\":\"ok\",\"game_ready\":{gameReady}}}");
                }
                else
                    SendJson(resp, 404, "{\"error\":\"Not Found\"}");
            }
            catch (Exception ex)
            {
                Log("[RewardHttpServer] Handler error: " + ex.Message);
                try
                {
                    SendJson(resp, 500, "{\"error\":\"Internal Server Error\"}");
                }
                catch
                {
                }
            }
        }

        private static void HandleReward(HttpListenerRequest req, HttpListenerResponse resp)
        {
            // Lee el JSON enviado en el body de la solicitud.
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
            var body = reader.ReadToEnd();

            // Convierte el JSON y valida los datos básicos.
            RewardPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<RewardPayload>(body);
            }
            catch
            {
                SendJson(resp, 400, "{\"error\":\"Invalid JSON\"}");
                return;
            }

            if (payload == null)
            {
                SendJson(resp, 400, "{\"error\":\"Empty body\"}");
                return;
            }

            // La validación específica del reward corresponde al mod.
            if (payload.RewardId <= 0)
            {
                SendJson(resp, 400, "{\"error\":\"'reward_id' must be a positive integer\"}");
                return;
            }

            // No acepta canjes si no hay una partida disponible.
            // El cliente puede consultar GET /health y reintentar cuando game_ready sea true.
            if (!_gameReady)
            {
                SendJson(resp, 503,
                    "{\"error\":\"game_not_ready\",\"summary\":\"The game is not ready to apply rewards.\"}");
                return;
            }

            // Encola el canje y espera el resultado que produzca el hilo principal en el próximo tick.
            var pendingReward = new PendingReward(payload.RewardId);
            PendingRewards.Enqueue(pendingReward);
            Log($"[RewardHttpServer] Reward queued -> reward_id={payload.RewardId}");

            if (!pendingReward.Completion.Task.Wait(RewardApplicationTimeout))
            {
                SendJson(resp, 504,
                    "{\"error\":\"application_timeout\",\"summary\":\"Could not confirm reward result.\"}");
                return;
            }

            RewardApplicationResult result = pendingReward.Completion.Task.Result;
            if (result.Succeeded)
            {
                SendJson(resp, 200, JsonSerializer.Serialize(new
                {
                    ok = true
                }));
                return;
            }

            SendJson(resp, 422, JsonSerializer.Serialize(new
            {
                ok = false,
                error = result.ErrorCode,
                summary = result.Summary
            }));
        }


        // Helpers
        private static void Log(string message) => _logger?.Invoke(message);

        private static void SendJson(HttpListenerResponse resp, int statusCode, string json)
        {
            resp.StatusCode = statusCode;
            resp.ContentType = "application/json";
            byte[] buf = Encoding.UTF8.GetBytes(json);
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.OutputStream.Close();
        }

        // Datos esperados en POST /reward.
        private sealed class RewardPayload
        {
            [JsonPropertyName("reward_id")] public int RewardId { get; set; }
        }

        // Une el reward recibido con la respuesta HTTP que debe esperar su resultado.
        private sealed class PendingReward
        {
            public int RewardId { get; }

            public TaskCompletionSource<RewardApplicationResult> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public PendingReward(int rewardId)
            {
                RewardId = rewardId;
            }
        }
    }
}
