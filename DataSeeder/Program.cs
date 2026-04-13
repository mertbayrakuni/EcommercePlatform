using Bogus;
using Npgsql;

var host = args.ElementAtOrDefault(0) ?? Environment.GetEnvironmentVariable("DB_HOST") ?? "127.0.0.1";
var port = args.ElementAtOrDefault(1) ?? Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
string Conn(string db) => $"Host={host};Port={port};Database={db};Username=postgres;Password=postgres";

Console.WriteLine($"Seeding databases at {host}:{port}...\n");

var adminPassword   = BCrypt.Net.BCrypt.HashPassword("Admin1234!");
var defaultPassword = BCrypt.Net.BCrypt.HashPassword("Password123!");

var customers  = new List<(int Id, string Email)>();
var categories = new List<int>();
var products   = new List<(int Id, decimal Price, string Sku, string Name)>();

var payments = new List<PaymentSeed>();

// ── 1. userdb ─────────────────────────────────────────────────────────────────
Console.Write("  Users       ");
await using (var conn = new NpgsqlConnection(Conn("userdb")))
{
    await conn.OpenAsync();
    await Exec(conn, """TRUNCATE TABLE "Users" CASCADE;""");

    var faker = new Faker("en");

    // Admin — İpek, intentionally excluded from the customer order pool
    await Insert(conn,
        """INSERT INTO "Users" ("FirstName","LastName","Email","PasswordHash","Role","CreatedAt","UpdatedAt") VALUES (@f,@l,@e,@pw,'Admin',@dt,@dt) RETURNING "Id";""",
        ("f", "İpek"), ("l", "Bayrak"), ("e", "ipek@bambicim.com"),
        ("pw", adminPassword), ("dt", DateTime.UtcNow));

    var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ipek@bambicim.com" };

    for (var i = 0; i < 100; i++)
    {
        string email;
        do { email = faker.Internet.Email().ToLower(); } while (!usedEmails.Add(email));

        var createdAt = DateTime.UtcNow.AddDays(-faker.Random.Int(0, 180));
        var id = await Insert(conn,
            """INSERT INTO "Users" ("FirstName","LastName","Email","PasswordHash","Role","CreatedAt","UpdatedAt") VALUES (@f,@l,@e,@pw,'Customer',@dt,@dt) RETURNING "Id";""",
            ("f", faker.Name.FirstName()), ("l", faker.Name.LastName()),
            ("e", email), ("pw", defaultPassword), ("dt", createdAt));

        customers.Add((id, email));
    }
}
Console.WriteLine($"1 admin + {customers.Count} customers ✓");

// ── 2. catalogdb ──────────────────────────────────────────────────────────────
Console.Write("  Catalog     ");
await using (var conn = new NpgsqlConnection(Conn("catalogdb")))
{
    await conn.OpenAsync();
    await Exec(conn, """TRUNCATE TABLE "Categories","Products" CASCADE;""");

    var faker = new Faker("en");

    string[] categoryNames =
    [
        "Electronics", "Clothing", "Books", "Home & Kitchen", "Sports & Outdoors",
        "Toys & Games", "Beauty & Health", "Automotive", "Garden & Tools", "Pet Supplies"
    ];

    foreach (var name in categoryNames)
    {
        var slug = name.ToLower().Replace(" & ", "-and-").Replace(" ", "-");
        var id = await Insert(conn,
            """INSERT INTO "Categories" ("Name","Slug","IsActive") VALUES (@n,@s,true) RETURNING "Id";""",
            ("n", name), ("s", slug));
        categories.Add(id);
    }

    var usedSkus = new HashSet<string>();
    for (var i = 0; i < 100; i++)
    {
        string sku;
        do { sku = faker.Commerce.Ean8(); } while (!usedSkus.Add(sku));

        var price     = Math.Round(faker.Random.Decimal(9.99m, 499.99m), 2);
        var name      = faker.Commerce.ProductName();
        var catId     = faker.PickRandom(categories);
        var createdAt = DateTime.UtcNow.AddDays(-faker.Random.Int(0, 365));

        var id = await Insert(conn,
            """INSERT INTO "Products" ("Name","Description","Price","Sku","ImageUrl","Stock","IsActive","CategoryId","CreatedAt","UpdatedAt") VALUES (@n,@d,@p,@sku,@img,@s,true,@cid,@dt,@dt) RETURNING "Id";""",
            ("n", name), ("d", faker.Commerce.ProductDescription()), ("p", price),
            ("sku", sku), ("img", $"https://picsum.photos/seed/{sku}/400/400"),
            ("s", faker.Random.Int(0, 500)), ("cid", catId), ("dt", createdAt));

        products.Add((id, price, sku, name));
    }
}
Console.WriteLine($"{categories.Count} categories, {products.Count} products ✓");

// ── 3. orderdb ────────────────────────────────────────────────────────────────
Console.Write("  Orders      ");
await using (var conn = new NpgsqlConnection(Conn("orderdb")))
{
    await conn.OpenAsync();
    await Exec(conn, """TRUNCATE TABLE "Orders","OrderItems" CASCADE;""");

    var faker = new Faker("en");

    // ~60% succeed (visa repeated 3×), ~40% decline — weighted by repetition
    (string Method, bool Ok, string? Reason)[] cards =
    [
        ("pm_card_visa",                            true,  null),
        ("pm_card_visa",                            true,  null),
        ("pm_card_visa",                            true,  null),
        ("pm_card_chargeDeclined",                  false, "Your card was declined."),
        ("pm_card_chargeDeclinedInsufficientFunds", false, "Insufficient funds."),
        ("pm_card_authenticationRequired",          false, "Authentication required."),
    ];

    for (var i = 0; i < 200; i++)
    {
        var customer  = faker.PickRandom(customers);
        var card      = faker.PickRandom(cards);
        var createdAt = DateTime.UtcNow.AddDays(-faker.Random.Double(0, 90));
        var daysSince = (DateTime.UtcNow - createdAt).TotalDays;

        // 1–4 unique products, qty 1–3 each
        var itemCount = faker.Random.Int(1, 4);
        var chosen    = faker.PickRandom(products, itemCount).Distinct().ToList();
        var lineItems = chosen.Select(p =>
        {
            var qty = faker.Random.Int(1, 3);
            return (Prod: p, Qty: qty, LineTotal: Math.Round(p.Price * qty, 2));
        }).ToList();

        var total = lineItems.Sum(l => l.LineTotal);

        // Status ages naturally for succeeded cards; failed cards stay Pending or get Cancelled
        string status = !card.Ok
            ? faker.Random.WeightedRandom(new[] { "Pending", "Cancelled" }, new[] { 0.35f, 0.65f })
            : daysSince < 2  ? "Paid"
            : daysSince < 14 ? faker.Random.WeightedRandom(new[] { "Paid", "Shipped" },            new[] { 0.4f, 0.6f })
            : daysSince < 45 ? faker.Random.WeightedRandom(new[] { "Shipped", "Delivered" },       new[] { 0.3f, 0.7f })
            :                  "Delivered";

        var updatedAt = status switch
        {
            "Paid"      => createdAt.AddMinutes(faker.Random.Double(5, 30)),
            "Shipped"   => createdAt.AddDays(faker.Random.Double(0.5, 2)),
            "Delivered" => createdAt.AddDays(faker.Random.Double(3, 10)),
            "Cancelled" => createdAt.AddHours(faker.Random.Double(1, 24)),
            _           => createdAt
        };

        var orderId = await Insert(conn,
            """INSERT INTO "Orders" ("CustomerEmail","TotalAmount","Status","CreatedAt","UpdatedAt") VALUES (@c,@t,@st,@dt,@upd) RETURNING "Id";""",
            ("c", customer.Email), ("t", total), ("st", status),
            ("dt", createdAt), ("upd", updatedAt));

        foreach (var (prod, qty, lineTotal) in lineItems)
        {
            await Exec(conn,
                """INSERT INTO "OrderItems" ("OrderId","ProductId","ProductName","ProductSku","UnitPrice","Quantity","LineTotal") VALUES (@oid,@pid,@pn,@psku,@up,@qty,@lt);""",
                ("oid", orderId), ("pid", prod.Id), ("pn", prod.Name),
                ("psku", prod.Sku), ("up", prod.Price), ("qty", qty), ("lt", lineTotal));
        }

        var txId        = card.Ok ? "tx_" + faker.Random.AlphaNumeric(16) : string.Empty;
        var processedAt = updatedAt.AddSeconds(faker.Random.Int(5, 90));
        payments.Add(new PaymentSeed(orderId, total, txId, card.Method, card.Ok, card.Reason, processedAt));
    }
}
Console.WriteLine($"{payments.Count} orders ✓");

// ── 4. paymentdb ──────────────────────────────────────────────────────────────
Console.Write("  Payments    ");
await using (var conn = new NpgsqlConnection(Conn("paymentdb")))
{
    await conn.OpenAsync();
    await Exec(conn, """TRUNCATE TABLE "Payments" CASCADE;""");

    foreach (var p in payments)
    {
        await Exec(conn,
            """INSERT INTO "Payments" ("OrderId","TransactionId","Amount","Method","Succeeded","ErrorMessage","ProcessedAt") VALUES (@o,@t,@a,@m,@s,@err,@dt);""",
            ("o", p.OrderId), ("t", p.TransactionId), ("a", p.Amount),
            ("m", p.Method), ("s", p.Succeeded),
            ("err", (object?)p.ErrorMsg ?? DBNull.Value),
            ("dt", p.ProcessedAt));
    }
}
Console.WriteLine($"{payments.Count} records ✓");

Console.WriteLine();
Console.WriteLine("✅  Done!\n");
Console.WriteLine("  Admin login     →  ipek@bambicim.com  /  Admin1234!");
Console.WriteLine("  Customer login  →  any seeded email   /  Password123!");

// ── Helpers ───────────────────────────────────────────────────────────────────
static async Task Exec(NpgsqlConnection conn, string sql, params (string Name, object? Value)[] ps)
{
    await using var cmd = new NpgsqlCommand(sql, conn);
    foreach (var (n, v) in ps)
        cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
    await cmd.ExecuteNonQueryAsync();
}

static async Task<int> Insert(NpgsqlConnection conn, string sql, params (string Name, object? Value)[] ps)
{
    await using var cmd = new NpgsqlCommand(sql, conn);
    foreach (var (n, v) in ps)
        cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
    return (int)(await cmd.ExecuteScalarAsync() ?? 0);
}

record PaymentSeed(int OrderId, decimal Amount, string TransactionId, string Method, bool Succeeded, string? ErrorMsg, DateTime ProcessedAt);
