#nullable enable

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace bGamesMod.bGamesModServices
{
	/// <summary>
	/// Servidor HTTP liviano que corre en segundo plano.
	/// Expone: POST /reward con un payload JSON que incluye <c>buff_id</c>.
	/// Es agnóstico a la lógica de dominio: solo recibe, valida y deja el valor pendiente
	/// para que el callback registrado en <see cref="Start"/> lo procese en el main thread.
	/// </summary>
	internal static class RewardHttpServer
	{
		private const int Port = 7878;
		private const int NoPendingValue = -1;

		// Usado por HTTP thread para escuchar requests. Necesita estar set para que ListenLoop() funcione.
		private static HttpListener? _listener;

		// Referencia al HTTP thread. Se inicia en Start() y se detiene en Stop().
		private static Thread? _thread;

		// Callback registrado por el aplicativo. Se invoca desde main thread via DrainPending().
		private static Action<int>? _onReward;

		// _logger:      Logger compartido. Usado por ambos threads para logging.
		private static Action<string>? _logger;

		// Estado compartido entre threads: guarda el reward pendiente hasta que DrainPending() lo consuma.
		private static volatile int _pendingValue = NoPendingValue;

		// Inicia el listener HTTP en un thread en segundo plano.
		public static void Start(Action<int> onReward, Action<string>? logger = null)
		{
			if (_listener != null)
				return;

			_onReward = onReward ?? throw new ArgumentNullException(nameof(onReward));
			_logger = logger ?? Console.WriteLine;

			_listener = new HttpListener();
			_listener.Prefixes.Add($"http://localhost:{Port}/");
			_listener.Start();

			// El listener loop vive en un thread separado para no bloquear el main thread del juego.
			// Ese thread solo atiende I/O HTTP y deja el efecto final pendiente para DrainPending().
			_thread = new Thread(ListenLoop) {
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
				// ignore shutdown errors
			}
			finally
			{
				_listener = null;
				_thread = null;
				_onReward = null;
				_logger = null;
				_pendingValue = NoPendingValue;
				// Limpia el estado compartido para que un reinicio arranque desde cero.
			}
		}

		// Transfiere cualquier _pendingValue recibido por el HTTP thread al main thread e invoca el callback registrado.
		// Debe ser llamado en cada game loop.
		public static void DrainPending()
		{
			int value = _pendingValue;
			if (value == NoPendingValue)
				return;

			// Reset antes de invocar para permitir que llegue un nuevo request inmediatamente.
			_pendingValue = NoPendingValue;

			// El callback corre en el main thread de la aplicación, donde es seguro tocar estado compartido.
			_onReward?.Invoke(value);
		}

		// Background listener loop (bloqueante). Razon por la que el HTTP server corre en un thread separado.
		private static void ListenLoop()
		{
			while (true)
			{
				HttpListenerContext ctx;
				try
				{
					// bloquea hasta que llegue un request.
					if (_listener is null)
						break;

					ctx = _listener.GetContext();
				}
				catch (HttpListenerException)
				{
					// Este error ocurre cuando se llama a Stop() y el listener se cierra mientras GetContext() está bloqueando.
					break;
				}
				catch (Exception ex)
				{
					Log("[RewardHttpServer] Listener error: " + ex.Message);
					break;
				}

				HandleContext(ctx);
			}
		}

		// Punto de entrada para cada request HTTP: decide si corresponde al endpoint /reward o responde 404.
		private static void HandleContext(HttpListenerContext ctx)
		{
			var req = ctx.Request;
			var resp = ctx.Response;

			try
			{
				// Router mínimo: solo POST /reward entra al flujo de recompensas.
				bool isReward = req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
				                && req.Url is not null
				                && req.Url.AbsolutePath.TrimEnd('/')
					                .Equals("/reward", StringComparison.OrdinalIgnoreCase);

				if (isReward)
					HandleReward(req, resp);
				else
					SendJson(resp, 404, "{\"error\":\"Not Found\"}");
			}
			catch (Exception ex)
			{
				Log("[RewardHttpServer] Handler error: " + ex.Message);
				try { SendJson(resp, 500, "{\"error\":\"Internal Server Error\"}"); }
				catch { }
			}
		}

		private static void HandleReward(HttpListenerRequest req, HttpListenerResponse resp)
		{
			// Read body: el JSON del endpoint debe llegar en el request body.
			string body;
			using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
				body = reader.ReadToEnd();

			// Parse JSON y aplicar validación básica del payload.
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

			// Sanity check basico, la validación de dominio más específica queda en el callback del mod.
			if (payload.BuffId <= 0)
			{
				SendJson(resp, 400, "{\"error\":\"'buff_id' must be a positive integer\"}");
				return;
			}

			// Señala al main thread, si llega otra request antes de DrainPending(), este valor se sobreescribe.
			_pendingValue = payload.BuffId;
			Log($"[RewardHttpServer] Reward queued -> buff_id={payload.BuffId}");

			SendJson(resp, 200, $"{{\"ok\":true,\"value\":{payload.BuffId}}}");
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

		// DTO para deserializar el payload del POST /reward.
		private sealed class RewardPayload
		{
			[JsonPropertyName("buff_id")] public int BuffId { get; set; }
		}
	}
}