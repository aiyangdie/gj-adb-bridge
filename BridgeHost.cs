using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GjBridge
{
    internal sealed class QrSession
    {
        public string Id;
        public string Name;
        public string Password;
        public string Payload;
        public string Status = "waiting";
        public string Message = "请用手机扫描二维码";
        public string Error;
        public string Device;
        public long Created;
    }

    internal sealed class BridgeHost
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly Dictionary<string, QrSession> _qr = new Dictionary<string, QrSession>();
        private readonly object _qrLock = new object();
        private readonly string[] _origins;
        private CancellationTokenSource _cts;
        private Task _loop;

        public int Port { get; private set; }
        public string Token { get; private set; }
        public bool IsRunning { get; private set; }
        public string LastError { get; private set; }

        public string PublicUrl
        {
            get { return string.Format("http://127.0.0.1:{0}/?token={1}", Port, Token); }
        }

        public BridgeHost()
        {
            Port = Program.DefaultPort;
            string envPort = Environment.GetEnvironmentVariable("PORT");
            int p;
            if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out p) && p > 0 && p < 65536)
                Port = p;

            string envOrigins = Environment.GetEnvironmentVariable("ADB_BRIDGE_ORIGINS");
            if (string.IsNullOrEmpty(envOrigins))
            {
                _origins = new string[]
                {
                    "https://sj.aikex.ink",
                    "http://127.0.0.1:8124",
                    "http://localhost:8124",
                    "http://127.0.0.1:17855",
                    "http://localhost:17855"
                };
            }
            else
            {
                string[] parts = envOrigins.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
                _origins = parts;
            }

            Token = LoadOrCreateToken();
        }

        private static string TokenFilePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "gj-toolbox-bridge");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "token.txt");
        }

        private static string LoadOrCreateToken()
        {
            string env = (Environment.GetEnvironmentVariable("ADB_BRIDGE_TOKEN") ?? "").Trim();
            if (env.Length > 0) return env;
            try
            {
                string path = TokenFilePath();
                if (File.Exists(path))
                {
                    string t = (File.ReadAllText(path) ?? "").Trim();
                    if (t.Length >= 16) return t;
                }
                byte[] buf = new byte[12];
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(buf);
                string token = BitConverter.ToString(buf).Replace("-", "").ToLowerInvariant();
                File.WriteAllText(path, token);
                return token;
            }
            catch
            {
                return Guid.NewGuid().ToString("N").Substring(0, 24);
            }
        }

        public void Start()
        {
            if (IsRunning) return;
            LastError = null;
            AdbRunner.EnsureResolved();
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", Port));
            try
            {
                _listener.Start();
            }
            catch (Exception ex)
            {
                LastError = "无法监听 127.0.0.1:" + Port + " — " + ex.Message;
                throw new Exception(LastError);
            }
            _cts = new CancellationTokenSource();
            IsRunning = true;
            _loop = Task.Factory.StartNew(() => AcceptLoop(_cts.Token), _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Stop()
        {
            IsRunning = false;
            try { if (_cts != null) _cts.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }

        private async void AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && IsRunning)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { if (!IsRunning) break; continue; }
                catch { if (!IsRunning) break; continue; }

                // fire-and-forget：每个 HTTP/WS 请求独立处理，不阻塞 Accept 循环
                Task ignored = Task.Factory.StartNew(() => HandleContext(ctx), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                if (ignored == null) { }
            }
        }

        private async void HandleContext(HttpListenerContext ctx)
        {
            try
            {
                if (!CheckOrigin(ctx.Request))
                {
                    await WriteJson(ctx.Response, 403, "{\"error\":\"Origin 不在白名单\"}").ConfigureAwait(false);
                    return;
                }

                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    AddCors(ctx.Response);
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                string path = ctx.Request.Url != null ? ctx.Request.Url.AbsolutePath : "/";
                if (path == "/health")
                {
                    AdbRunner.EnsureResolved();
                    string body = string.Format(
                        "{{\"ok\":true,\"name\":\"gj-adb-bridge\",\"version\":\"{0}\",\"tokenRequired\":true,\"token\":\"{1}\",\"url\":\"{2}\",\"port\":{3},\"adb\":\"{4}\",\"adbOk\":{5},\"ui\":\"tray\"}}",
                        Program.Version,
                        JsonEscape(Token),
                        JsonEscape(PublicUrl),
                        Port,
                        JsonEscape(AdbRunner.AdbPath),
                        AdbRunner.AdbOk ? "true" : "false");
                    await WriteJson(ctx.Response, 200, body).ConfigureAwait(false);
                    return;
                }

                if (ctx.Request.IsWebSocketRequest)
                {
                    if (!CheckToken(ctx.Request.Url))
                    {
                        ctx.Response.StatusCode = 401;
                        ctx.Response.Close();
                        return;
                    }
                    await HandleWebSocket(ctx).ConfigureAwait(false);
                    return;
                }

                if (!CheckToken(ctx.Request.Url))
                {
                    await WriteJson(ctx.Response, 401, "{\"error\":\"token 无效，请使用启动时打印的完整地址\"}").ConfigureAwait(false);
                    return;
                }

                await HandleApi(ctx, path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try { WriteJsonSync(ctx.Response, 500, "{\"error\":\"" + JsonEscape(ex.Message) + "\"}"); }
                catch { }
            }
        }

        private bool CheckOrigin(HttpListenerRequest req)
        {
            string origin = req.Headers["Origin"];
            if (string.IsNullOrEmpty(origin)) return true;
            for (int i = 0; i < _origins.Length; i++)
            {
                if (string.Equals(_origins[i], origin, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool CheckToken(Uri url)
        {
            if (url == null) return false;
            string q = url.Query ?? "";
            if (q.StartsWith("?")) q = q.Substring(1);
            string token = "";
            string[] parts = q.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] kv = parts[i].Split(new char[] { '=' }, 2);
                if (kv.Length == 2 && kv[0] == "token")
                {
                    token = Uri.UnescapeDataString(kv[1] ?? "");
                    break;
                }
            }
            return FixedEqual(token, Token);
        }

        private static bool FixedEqual(string a, string b)
        {
            if (a == null) a = "";
            if (b == null) b = "";
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static bool IsPrivateHost(string host)
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1")
                return true;
            Match m = Regex.Match(host ?? "", @"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$");
            if (!m.Success) return false;
            int a = int.Parse(m.Groups[1].Value);
            int b = int.Parse(m.Groups[2].Value);
            if (a == 10 || a == 127) return true;
            if (a == 192 && b == 168) return true;
            if (a == 172 && b >= 16 && b <= 31) return true;
            if (a == 169 && b == 254) return true;
            return false;
        }

        private async Task HandleApi(HttpListenerContext ctx, string path)
        {
            string method = ctx.Request.HttpMethod ?? "GET";

            if (method == "POST" && path == "/start-server")
            {
                AdbRunner.Run(new string[] { "start-server" }, 30000);
                await WriteJson(ctx.Response, 200, "{\"ok\":true}").ConfigureAwait(false);
                return;
            }

            if ((method == "GET" || method == "POST") && path == "/devices")
            {
                try { AdbRunner.Run(new string[] { "start-server" }, 30000); } catch { }
                AdbResult r = AdbRunner.Run(new string[] { "devices", "-l" }, 30000);
                List<Dictionary<string, string>> devices = AdbRunner.ParseDevices(r.StdOut);
                StringBuilder sb = new StringBuilder();
                sb.Append("{\"ok\":true,\"devices\":[");
                for (int i = 0; i < devices.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Dictionary<string, string> d = devices[i];
                    sb.Append('{');
                    sb.Append("\"serial\":\"").Append(JsonEscape(d["serial"])).Append("\",");
                    sb.Append("\"state\":\"").Append(JsonEscape(d["state"])).Append("\",");
                    sb.Append("\"model\":\"").Append(JsonEscape(d["model"])).Append("\",");
                    sb.Append("\"product\":\"").Append(JsonEscape(d["product"])).Append("\",");
                    sb.Append("\"transportId\":\"").Append(JsonEscape(d["transportId"])).Append("\",");
                    sb.Append("\"kind\":\"").Append(JsonEscape(d["kind"])).Append("\"");
                    sb.Append('}');
                }
                sb.Append("],\"raw\":\"").Append(JsonEscape((r.StdOut ?? "").Trim())).Append("\"}");
                await WriteJson(ctx.Response, 200, sb.ToString()).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path == "/pair")
            {
                string body = ReadBody(ctx.Request);
                string address = JsonGet(body, "address");
                string code = JsonGet(body, "code");
                if (!Regex.IsMatch(address ?? "", @"^(?:\d{1,3}\.){3}\d{1,3}:\d{1,5}$"))
                {
                    await WriteJson(ctx.Response, 400, "{\"error\":\"配对地址格式应为 IP:端口\"}").ConfigureAwait(false);
                    return;
                }
                if (!Regex.IsMatch(code ?? "", @"^\d{6}$"))
                {
                    await WriteJson(ctx.Response, 400, "{\"error\":\"配对码应为 6 位数字\"}").ConfigureAwait(false);
                    return;
                }
                string host = address.Split(':')[0];
                if (!IsPrivateHost(host))
                {
                    await WriteJson(ctx.Response, 400, "{\"error\":\"仅允许局域网地址\"}").ConfigureAwait(false);
                    return;
                }
                AdbResult result = AdbRunner.Run(new string[] { "pair", address, code }, 60000);
                await WriteJson(ctx.Response, 200, "{\"ok\":true,\"output\":\"" + JsonEscape(result.Combined) + "\"}").ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path == "/connect")
            {
                string body = ReadBody(ctx.Request);
                string address = JsonGet(body, "address");
                if (!Regex.IsMatch(address ?? "", @"^(?:\d{1,3}\.){3}\d{1,3}:\d{1,5}$"))
                {
                    await WriteJson(ctx.Response, 400, "{\"error\":\"连接地址格式应为 IP:端口\"}").ConfigureAwait(false);
                    return;
                }
                if (!IsPrivateHost(address.Split(':')[0]))
                {
                    await WriteJson(ctx.Response, 400, "{\"error\":\"仅允许局域网地址\"}").ConfigureAwait(false);
                    return;
                }
                AdbResult result = AdbRunner.Run(new string[] { "connect", address }, 30000);
                await WriteJson(ctx.Response, 200, "{\"ok\":true,\"output\":\"" + JsonEscape(result.Combined) + "\"}").ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path == "/qr-pair/start")
            {
                byte[] nb = new byte[3];
                byte[] idb = new byte[8];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(nb);
                    rng.GetBytes(idb);
                }
                string name = "studio-" + BitConverter.ToString(nb).Replace("-", "").ToLowerInvariant();
                string password = (100000 + new Random().Next(900000)).ToString();
                string payload = string.Format("WIFI:T:ADB;S:{0};P:{1};;", name, password);
                string id = BitConverter.ToString(idb).Replace("-", "").ToLowerInvariant();
                QrSession session = new QrSession();
                session.Id = id;
                session.Name = name;
                session.Password = password;
                session.Payload = payload;
                session.Created = DateTime.UtcNow.Ticks;
                lock (_qrLock)
                {
                    List<string> dead = new List<string>();
                    foreach (KeyValuePair<string, QrSession> kv in _qr)
                    {
                        if ((DateTime.UtcNow.Ticks - kv.Value.Created) > TimeSpan.FromMinutes(3).Ticks)
                            dead.Add(kv.Key);
                    }
                    for (int i = 0; i < dead.Count; i++) _qr.Remove(dead[i]);
                    _qr[id] = session;
                }
                ThreadPool.QueueUserWorkItem(delegate { RunQrPairLoop(session); });
                string resp = string.Format(
                    "{{\"ok\":true,\"sessionId\":\"{0}\",\"name\":\"{1}\",\"password\":\"{2}\",\"qr\":\"{3}\",\"hint\":\"手机：开发者选项 → 无线调试 → 使用二维码配对设备\"}}",
                    JsonEscape(id), JsonEscape(name), JsonEscape(password), JsonEscape(payload));
                await WriteJson(ctx.Response, 200, resp).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path == "/qr-pair/status")
            {
                string id = ctx.Request.QueryString["id"] ?? "";
                QrSession session = null;
                lock (_qrLock) { if (_qr.ContainsKey(id)) session = _qr[id]; }
                if (session == null)
                {
                    await WriteJson(ctx.Response, 404, "{\"error\":\"配对会话不存在或已过期\"}").ConfigureAwait(false);
                    return;
                }
                string resp = string.Format(
                    "{{\"ok\":true,\"sessionId\":\"{0}\",\"status\":\"{1}\",\"message\":\"{2}\",\"error\":{3},\"device\":{4},\"name\":\"{5}\",\"password\":\"{6}\",\"qr\":\"{7}\"}}",
                    JsonEscape(session.Id),
                    JsonEscape(session.Status),
                    JsonEscape(session.Message ?? ""),
                    session.Error == null ? "null" : ("\"" + JsonEscape(session.Error) + "\""),
                    session.Device == null ? "null" : ("\"" + JsonEscape(session.Device) + "\""),
                    JsonEscape(session.Name),
                    JsonEscape(session.Password),
                    JsonEscape(session.Payload));
                await WriteJson(ctx.Response, 200, resp).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path == "/qr-pair/cancel")
            {
                string body = ReadBody(ctx.Request);
                string id = JsonGet(body, "sessionId");
                if (string.IsNullOrEmpty(id)) id = JsonGet(body, "id");
                lock (_qrLock)
                {
                    if (_qr.ContainsKey(id))
                    {
                        QrSession s = _qr[id];
                        if (s.Status == "waiting")
                        {
                            s.Status = "cancelled";
                            s.Error = "已取消";
                        }
                    }
                }
                await WriteJson(ctx.Response, 200, "{\"ok\":true}").ConfigureAwait(false);
                return;
            }

            await WriteJson(ctx.Response, 404, "{\"error\":\"not found\"}").ConfigureAwait(false);
        }

        private void RunQrPairLoop(QrSession session)
        {
            try
            {
                DateTime deadline = DateTime.UtcNow.AddMinutes(2);
                try { AdbRunner.Run(new string[] { "start-server" }, 30000); } catch { }
                try { AdbRunner.Run(new string[] { "mdns", "check" }, 10000); } catch { }

                while (DateTime.UtcNow < deadline && session.Status == "waiting")
                {
                    List<Dictionary<string, string>> services = new List<Dictionary<string, string>>();
                    try
                    {
                        AdbResult r = AdbRunner.Run(new string[] { "mdns", "services" }, 15000);
                        services = AdbRunner.ParseMdns(r.StdOut);
                    }
                    catch (Exception ex)
                    {
                        session.Message = "等待手机扫码…（mdns: " + ex.Message + "）";
                        Thread.Sleep(1500);
                        continue;
                    }

                    Dictionary<string, string> pairSvc = null;
                    for (int i = 0; i < services.Count; i++)
                    {
                        Dictionary<string, string> s = services[i];
                        string type = s.ContainsKey("type") ? s["type"] : "";
                        string name = s.ContainsKey("name") ? s["name"] : "";
                        if (Regex.IsMatch(type, "adb-tls-pairing", RegexOptions.IgnoreCase) &&
                            (name == session.Name || name.StartsWith(session.Name)))
                        {
                            pairSvc = s;
                            break;
                        }
                    }

                    if (pairSvc == null)
                    {
                        session.Message = "等待手机扫码…";
                        Thread.Sleep(1200);
                        continue;
                    }

                    session.Status = "pairing";
                    session.Message = string.Format("发现配对服务 {0}:{1}，正在配对", pairSvc["host"], pairSvc["port"]);
                    try
                    {
                        AdbRunner.Run(new string[] { "pair", pairSvc["host"] + ":" + pairSvc["port"], session.Password }, 60000);
                    }
                    catch (Exception ex)
                    {
                        session.Status = "error";
                        session.Error = ex.Message;
                        return;
                    }

                    session.Status = "connecting";
                    session.Message = "配对成功，正在连接…";
                    string connected = null;
                    for (int i = 0; i < 25; i++)
                    {
                        try
                        {
                            AdbResult r = AdbRunner.Run(new string[] { "mdns", "services" }, 15000);
                            List<Dictionary<string, string>> svcs = AdbRunner.ParseMdns(r.StdOut);
                            for (int j = 0; j < svcs.Count; j++)
                            {
                                Dictionary<string, string> s = svcs[j];
                                if (Regex.IsMatch(s["type"], "adb-tls-connect", RegexOptions.IgnoreCase) && s["host"] == pairSvc["host"])
                                {
                                    AdbRunner.Run(new string[] { "connect", s["host"] + ":" + s["port"] }, 30000);
                                    connected = s["host"] + ":" + s["port"];
                                    break;
                                }
                            }
                            if (connected != null) break;
                        }
                        catch { }

                        try
                        {
                            AdbResult d = AdbRunner.Run(new string[] { "devices", "-l" }, 30000);
                            List<Dictionary<string, string>> list = AdbRunner.ParseDevices(d.StdOut);
                            for (int j = 0; j < list.Count; j++)
                            {
                                if (list[j]["state"] == "device" && list[j]["serial"].StartsWith(pairSvc["host"] + ":"))
                                {
                                    connected = list[j]["serial"];
                                    break;
                                }
                            }
                            if (connected != null) break;
                        }
                        catch { }
                        Thread.Sleep(1000);
                    }

                    if (connected == null)
                    {
                        try
                        {
                            AdbResult d = AdbRunner.Run(new string[] { "devices", "-l" }, 30000);
                            List<Dictionary<string, string>> list = AdbRunner.ParseDevices(d.StdOut);
                            for (int j = 0; j < list.Count; j++)
                            {
                                if (list[j]["state"] != "device") continue;
                                if (Regex.IsMatch(list[j]["serial"], @":\d+$") || connected == null)
                                    connected = list[j]["serial"];
                                if (Regex.IsMatch(list[j]["serial"], @":\d+$")) break;
                            }
                        }
                        catch { }
                    }

                    session.Device = connected;
                    session.Status = "done";
                    session.Message = connected != null ? ("已配对并连接 " + connected) : "已配对，请在列表中刷新选择";
                    return;
                }

                if (session.Status == "waiting")
                {
                    session.Status = "timeout";
                    session.Error = "等待扫码超时。请确认手机与电脑同一 Wi-Fi，路由器未开 AP 隔离，并使用「无线调试 → 二维码配对」。";
                }
            }
            catch (Exception ex)
            {
                session.Status = "error";
                session.Error = ex.Message;
            }
        }

        private async Task HandleWebSocket(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath;
            HttpListenerWebSocketContext wsCtx = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
            WebSocket ws = wsCtx.WebSocket;

            if (path == "/server")
            {
                await PipeTcp(ws, "127.0.0.1", 5037).ConfigureAwait(false);
                return;
            }

            if (path == "/adb")
            {
                string host = ctx.Request.QueryString["host"] ?? "";
                int port = 0;
                int.TryParse(ctx.Request.QueryString["port"] ?? "0", out port);
                if (!IsPrivateHost(host) || port < 1 || port > 65535)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "invalid target", CancellationToken.None).ConfigureAwait(false);
                    return;
                }
                await PipeTcp(ws, host, port).ConfigureAwait(false);
                return;
            }

            await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "unknown path", CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task PipeTcp(WebSocket ws, string host, int port)
        {
            TcpClient tcp = new TcpClient();
            tcp.NoDelay = true;
            Exception connectError = null;
            try
            {
                await Task.Factory.FromAsync(tcp.BeginConnect, tcp.EndConnect, host, port, null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                connectError = ex;
            }
            if (connectError != null)
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                        await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, connectError.Message, CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
                try { tcp.Close(); } catch { }
                return;
            }

            try { tcp.Client.NoDelay = true; } catch { }
            NetworkStream stream = tcp.GetStream();
            CancellationTokenSource cts = new CancellationTokenSource();
            Task t1 = WsToTcp(ws, tcp, stream, cts.Token);
            Task t2 = TcpToWs(ws, tcp, stream, cts.Token);
            try
            {
                Task first = await Task.WhenAny(t1, t2).ConfigureAwait(false);
                // 关键：一侧先结束时立刻 Cancel，会把还没读完的 ADB 响应（OKAY 后面的正文）掐掉
                // 正确做法：半关闭写端，给读端一点时间把剩余 TCP 数据刷到 WebSocket
                if (object.ReferenceEquals(first, t1) && !t2.IsCompleted)
                {
                    try { tcp.Client.Shutdown(SocketShutdown.Send); } catch { }
                    await Task.WhenAny(t2, Task.Delay(8000)).ConfigureAwait(false);
                }
                else if (object.ReferenceEquals(first, t2) && !t1.IsCompleted)
                {
                    try { tcp.Client.Shutdown(SocketShutdown.Receive); } catch { }
                    await Task.WhenAny(t1, Task.Delay(2000)).ConfigureAwait(false);
                }
            }
            finally
            {
                try { cts.Cancel(); } catch { }
                try { tcp.Close(); } catch { }
            }
            try
            {
                if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
            }
            catch { }
        }

        private static async Task WsToTcp(WebSocket ws, TcpClient tcp, NetworkStream stream, CancellationToken token)
        {
            byte[] buffer = new byte[64 * 1024];
            try
            {
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested && tcp.Connected)
                {
                    WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.Count <= 0) continue;
                    // 拷贝后再写，避免底层 WS 缓冲与 NetworkStream 并发踩内存
                    byte[] chunk = new byte[result.Count];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, result.Count);
                    await stream.WriteAsync(chunk, 0, chunk.Length, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (WebSocketException) { }
            catch (IOException) { }
            catch (Exception) { }
        }

        private static async Task TcpToWs(WebSocket ws, TcpClient tcp, NetworkStream stream, CancellationToken token)
        {
            byte[] buffer = new byte[64 * 1024];
            try
            {
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested && tcp.Connected)
                {
                    int n = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (n <= 0) break;

                    // 尽量把当前已到的 TCP 数据拼成一次 WS 发送，减少「只发出 OKAY」的窗口
                    using (MemoryStream ms = new MemoryStream(n + 256))
                    {
                        ms.Write(buffer, 0, n);
                        int spin = 0;
                        while (spin < 32)
                        {
                            int avail = 0;
                            try { avail = tcp.Available; } catch { avail = 0; }
                            if (avail <= 0) break;
                            int take = Math.Min(avail, buffer.Length);
                            int m = await stream.ReadAsync(buffer, 0, take, token).ConfigureAwait(false);
                            if (m <= 0) break;
                            ms.Write(buffer, 0, m);
                            spin++;
                        }
                        byte[] payload = ms.ToArray();
                        if (ws.State != WebSocketState.Open) break;
                        await ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (WebSocketException) { }
            catch (IOException) { }
            catch (Exception) { }
        }

        private static void AddCors(HttpListenerResponse res)
        {
            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            res.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
            res.Headers["Access-Control-Allow-Private-Network"] = "true";
        }

        private static async Task WriteJson(HttpListenerResponse res, int code, string body)
        {
            AddCors(res);
            res.StatusCode = code;
            res.ContentType = "application/json; charset=utf-8";
            byte[] bytes = Encoding.UTF8.GetBytes(body ?? "");
            res.ContentLength64 = bytes.Length;
            await res.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            res.Close();
        }

        private static void WriteJsonSync(HttpListenerResponse res, int code, string body)
        {
            AddCors(res);
            res.StatusCode = code;
            res.ContentType = "application/json; charset=utf-8";
            byte[] bytes = Encoding.UTF8.GetBytes(body ?? "");
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.Close();
        }

        private static string ReadBody(HttpListenerRequest req)
        {
            using (StreamReader sr = new StreamReader(req.InputStream, Encoding.UTF8))
                return sr.ReadToEnd() ?? "";
        }

        private static string JsonGet(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            if (m.Success) return UnescapeJson(m.Groups[1].Value);
            m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*([^,}\\s]+)");
            return m.Success ? m.Groups[1].Value.Trim('"') : "";
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static string JsonEscape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
