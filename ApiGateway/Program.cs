using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var jwtSecret = builder.Configuration["Jwt:Secret"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ApiGateway" }));

app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0" />
      <title>ECommerce Platform — Gateway</title>
      <style>
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Segoe UI', system-ui, sans-serif; background: #0d1117; color: #e6edf3; min-height: 100vh; }
        header { background: #161b22; border-bottom: 1px solid #30363d; padding: 18px 48px; display: flex; align-items: center; gap: 14px; }
        .logo { font-size: 1.2rem; font-weight: 700; color: #58a6ff; letter-spacing: -0.02em; }
        .logo span { color: #8b949e; font-weight: 400; }
        .badge-gw { background: #1f6feb22; color: #58a6ff; border: 1px solid #1f6feb55; padding: 3px 10px; border-radius: 20px; font-size: 0.7rem; font-weight: 600; letter-spacing: 0.05em; text-transform: uppercase; }
        .header-right { margin-left: auto; }
        .online-badge { font-size: 0.72rem; font-weight: 600; padding: 4px 14px; border-radius: 20px; background: #21262d; color: #8b949e; border: 1px solid #30363d; transition: background 0.4s, color 0.4s, border-color 0.4s; }
        .online-badge.all-ok   { background: #3fb95022; color: #3fb950; border-color: #3fb95044; }
        .online-badge.some-err { background: #e3b34122; color: #e3b341; border-color: #e3b34144; }
        .online-badge.all-err  { background: #f8514922; color: #f85149; border-color: #f8514944; }
        main { max-width: 1100px; margin: 0 auto; padding: 40px 48px; }
        .section { margin-bottom: 48px; }
        .section-label { font-size: 0.68rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.12em; color: #8b949e; margin-bottom: 14px; padding-bottom: 8px; border-bottom: 1px solid #21262d; }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; }
        .card { background: #161b22; border: 1px solid #30363d; border-radius: 10px; padding: 20px; transition: border-color 0.2s, transform 0.2s, box-shadow 0.2s; }
        .card-user:hover    { border-color: #58a6ff55; transform: translateY(-3px); box-shadow: 0 8px 24px #58a6ff0d; }
        .card-catalog:hover { border-color: #3fb95055; transform: translateY(-3px); box-shadow: 0 8px 24px #3fb9500d; }
        .card-order:hover   { border-color: #d2a8ff55; transform: translateY(-3px); box-shadow: 0 8px 24px #d2a8ff0d; }
        .card-payment:hover { border-color: #ff6c3755; transform: translateY(-3px); box-shadow: 0 8px 24px #ff6c370d; }
        .card-top { display: flex; align-items: center; gap: 10px; margin-bottom: 14px; }
        .dot { width: 8px; height: 8px; border-radius: 50%; background: #30363d; flex-shrink: 0; transition: background 0.4s, box-shadow 0.4s; }
        .dot.ok  { background: #3fb950; box-shadow: 0 0 7px #3fb95077; }
        .dot.err { background: #f85149; box-shadow: 0 0 7px #f8514977; }
        .svc-info { display: flex; flex-direction: column; gap: 1px; }
        .svc-name { font-weight: 600; font-size: 0.92rem; }
        .svc-desc { font-size: 0.64rem; color: #8b949e; }
        .port { font-family: 'Cascadia Code', 'Consolas', monospace; font-size: 0.68rem; color: #8b949e; background: #0d1117; padding: 2px 7px; border-radius: 4px; border: 1px solid #30363d; margin-left: auto; white-space: nowrap; }
        .endpoints { display: flex; flex-direction: column; gap: 5px; margin-bottom: 16px; min-height: 60px; }
        .ep { display: flex; align-items: center; gap: 7px; font-family: 'Cascadia Code', 'Consolas', monospace; font-size: 0.68rem; color: #8b949e; }
        .m { padding: 1px 5px; border-radius: 3px; font-size: 0.62rem; font-weight: 700; min-width: 40px; text-align: center; }
        .GET    { background: #1f6feb22; color: #58a6ff; }
        .POST   { background: #3fb95022; color: #3fb950; }
        .PATCH  { background: #e3b34122; color: #e3b341; }
        .DELETE { background: #f8514922; color: #f85149; }
        .btn-row { display: flex; gap: 6px; flex-wrap: wrap; }
        a.btn { display: inline-flex; align-items: center; gap: 4px; padding: 5px 11px; border-radius: 6px; font-size: 0.7rem; font-weight: 500; text-decoration: none; transition: opacity 0.15s, transform 0.1s; }
        a.btn:hover { opacity: 0.8; transform: translateY(-1px); }
        a.btn-scalar { background: #ff6c3722; color: #ff6c37; border: 1px solid #ff6c3744; }
        a.btn-spec   { background: #6e40c922; color: #d2a8ff; border: 1px solid #6e40c944; }
        a.btn-hl     { background: #3fb95022; color: #3fb950; border: 1px solid #3fb95044; }
        table { width: 100%; border-collapse: collapse; background: #161b22; border: 1px solid #30363d; border-radius: 10px; overflow: hidden; font-size: 0.82rem; }
        th { padding: 10px 16px; text-align: left; background: #0d1117; color: #8b949e; font-size: 0.68rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em; border-bottom: 1px solid #21262d; }
        td { padding: 10px 16px; border-bottom: 1px solid #21262d; }
        tr:last-child td { border-bottom: none; }
        tr:hover td { background: #1c2128; }
        td code { font-family: 'Cascadia Code', 'Consolas', monospace; font-size: 0.76rem; }
        .tag { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 0.66rem; font-weight: 600; }
        .tag-pub { background: #1f6feb22; color: #58a6ff; border: 1px solid #1f6feb44; }
        .tag-jwt { background: #6e40c922; color: #d2a8ff; border: 1px solid #6e40c944; }
        footer { padding: 20px 48px; border-top: 1px solid #21262d; }
        .footer-inner { display: flex; align-items: center; justify-content: center; gap: 12px; flex-wrap: wrap; color: #484f58; font-size: 0.72rem; }
        .footer-badge { background: #161b22; border: 1px solid #30363d; padding: 2px 10px; border-radius: 12px; font-size: 0.66rem; color: #8b949e; }
      </style>
    </head>
    <body>
      <header>
        <div class="logo">ECommerce Platform <span>/ API Gateway</span></div>
        <div class="badge-gw">:5000</div>
        <div class="header-right">
          <div class="online-badge" id="online-badge">— / 4 online</div>
        </div>
      </header>
      <main>
        <div class="section">
          <div class="section-label">Services</div>
          <div class="grid">
            <div class="card card-user">
              <div class="card-top">
                <div class="dot" id="d-user"></div>
                <div class="svc-info"><span class="svc-name">UserService</span><span class="svc-desc">Auth &amp; user management</span></div>
                <span class="port">:5101</span>
              </div>
              <div class="endpoints">
                <div class="ep"><span class="m POST">POST</span>/api/auth/register</div>
                <div class="ep"><span class="m POST">POST</span>/api/auth/login</div>
                <div class="ep"><span class="m GET">GET</span>/api/auth/me</div>
                <div class="ep"><span class="m PATCH">PATCH</span>/api/auth/{id}/role</div>
              </div>
              <div class="btn-row">
                <a class="btn btn-scalar" href="http://localhost:5101/scalar/v1" target="_blank">&#9670; Scalar</a>
                <a class="btn btn-spec" href="http://localhost:5101/openapi/v1.json" target="_blank">{ } Spec</a>
                <a class="btn btn-hl" href="http://localhost:5101/health" target="_blank">&#9825; Health</a>
              </div>
            </div>
            <div class="card card-catalog">
              <div class="card-top">
                <div class="dot" id="d-catalog"></div>
                <div class="svc-info"><span class="svc-name">CatalogService</span><span class="svc-desc">Products, categories &amp; inventory</span></div>
                <span class="port">:5098</span>
              </div>
              <div class="endpoints">
                <div class="ep"><span class="m GET">GET</span>/api/products</div>
                <div class="ep"><span class="m POST">POST</span>/api/products</div>
                <div class="ep"><span class="m GET">GET</span>/api/categories</div>
                <div class="ep"><span class="m GET">GET</span>/api/inventory</div>
              </div>
              <div class="btn-row">
                <a class="btn btn-scalar" href="http://localhost:5098/scalar/v1" target="_blank">&#9670; Scalar</a>
                <a class="btn btn-spec" href="http://localhost:5098/openapi/v1.json" target="_blank">{ } Spec</a>
                <a class="btn btn-hl" href="http://localhost:5098/health" target="_blank">&#9825; Health</a>
              </div>
            </div>
            <div class="card card-order">
              <div class="card-top">
                <div class="dot" id="d-order"></div>
                <div class="svc-info"><span class="svc-name">OrderService</span><span class="svc-desc">Order placement &amp; tracking</span></div>
                <span class="port">:5099</span>
              </div>
              <div class="endpoints">
                <div class="ep"><span class="m POST">POST</span>/api/orders</div>
                <div class="ep"><span class="m GET">GET</span>/api/orders</div>
                <div class="ep"><span class="m GET">GET</span>/api/orders/{id}</div>
                <div class="ep"><span class="m POST">POST</span>/api/orders/{id}/cancel</div>
              </div>
              <div class="btn-row">
                <a class="btn btn-scalar" href="http://localhost:5099/scalar/v1" target="_blank">&#9670; Scalar</a>
                <a class="btn btn-spec" href="http://localhost:5099/openapi/v1.json" target="_blank">{ } Spec</a>
                <a class="btn btn-hl" href="http://localhost:5099/health" target="_blank">&#9825; Health</a>
              </div>
            </div>
            <div class="card card-payment">
              <div class="card-top">
                <div class="dot" id="d-payment"></div>
                <div class="svc-info"><span class="svc-name">PaymentService</span><span class="svc-desc">Payment processing</span></div>
                <span class="port">:5100</span>
              </div>
              <div class="endpoints">
                <div class="ep"><span class="m POST">POST</span>/api/payments/pay</div>
              </div>
              <div class="btn-row">
                <a class="btn btn-scalar" href="http://localhost:5100/scalar/v1" target="_blank">&#9670; Scalar</a>
                <a class="btn btn-spec" href="http://localhost:5100/openapi/v1.json" target="_blank">{ } Spec</a>
                <a class="btn btn-hl" href="http://localhost:5100/health" target="_blank">&#9825; Health</a>
              </div>
            </div>
          </div>
        </div>
        <div class="section">
          <div class="section-label">Gateway Routes</div>
          <table>
            <thead><tr><th>Route</th><th>Path</th><th>Target</th><th>Auth</th></tr></thead>
            <tbody>
              <tr><td>auth</td><td><code>/api/auth/{**catch-all}</code></td><td>UserService :5101</td><td><span class="tag tag-pub">Public</span></td></tr>
              <tr><td>products</td><td><code>/api/products/{**catch-all}</code></td><td>CatalogService :5098</td><td><span class="tag tag-pub">Public</span></td></tr>
              <tr><td>categories</td><td><code>/api/categories/{**catch-all}</code></td><td>CatalogService :5098</td><td><span class="tag tag-pub">Public</span></td></tr>
              <tr><td>inventory</td><td><code>/api/inventory/{**catch-all}</code></td><td>CatalogService :5098</td><td><span class="tag tag-jwt">&#128274; JWT</span></td></tr>
              <tr><td>orders</td><td><code>/api/orders/{**catch-all}</code></td><td>OrderService :5099</td><td><span class="tag tag-jwt">&#128274; JWT</span></td></tr>
              <tr><td>payments</td><td><code>/api/payments/{**catch-all}</code></td><td>PaymentService :5100</td><td><span class="tag tag-jwt">&#128274; JWT</span></td></tr>
            </tbody>
          </table>
        </div>
      </main>
      <footer>
        <div class="footer-inner">
          <span>ECommerce Platform &nbsp;·&nbsp; API Gateway</span>
          <span class="footer-badge">YARP Reverse Proxy</span>
          <span class="footer-badge">.NET 10</span>
          <span class="footer-badge">Scalar 2.13</span>
        </div>
      </footer>
      <script>
        const svcs = [
          { id: 'd-user',    url: 'http://localhost:5101/health' },
          { id: 'd-catalog', url: 'http://localhost:5098/health' },
          { id: 'd-order',   url: 'http://localhost:5099/health' },
          { id: 'd-payment', url: 'http://localhost:5100/health' },
        ];
        async function ping(s) {
          const el = document.getElementById(s.id);
          try {
            const r = await fetch(s.url, { signal: AbortSignal.timeout(3000) });
            el.className = 'dot ' + (r.ok ? 'ok' : 'err');
            return r.ok;
          } catch { el.className = 'dot err'; return false; }
        }
        async function pingAll() {
          const results = await Promise.all(svcs.map(ping));
          const count = results.filter(Boolean).length;
          const badge = document.getElementById('online-badge');
          badge.textContent = count + ' / ' + svcs.length + ' online';
          badge.className = 'online-badge ' + (count === svcs.length ? 'all-ok' : count === 0 ? 'all-err' : 'some-err');
        }
        pingAll();
        setInterval(pingAll, 15000);
      </script>
    </body>
    </html>
    """, "text/html"));

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();

public partial class Program { }
