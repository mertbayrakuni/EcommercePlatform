using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Bogus;

namespace DataSeeder
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting data seeding...");
            var defaultPassword = BCrypt.Net.BCrypt.HashPassword("Password123!");
            var adminPassword = BCrypt.Net.BCrypt.HashPassword("Admin1234!");

            var users = new List<(int Id, string Email)>();
            var categories = new List<int>();
            var products = new List<(int Id, decimal Price, string Sku, string Name)>();
            var orderData = new List<(int OrderId, decimal Amount, string Method, bool Succeeded, string? ErrorMsg)>();

            // 1. userdb - Users
            await using (var conn = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=userdb;Username=postgres;Password=postgres"))
            {
                await conn.OpenAsync();
                await using (var cleanCmd = new NpgsqlCommand("TRUNCATE TABLE \"Users\" CASCADE;", conn)) await cleanCmd.ExecuteNonQueryAsync();

                var faker = new Faker("en");
                
                // Add Admin Account
                var adminSql = @"INSERT INTO ""Users"" (""FirstName"", ""LastName"", ""Email"", ""PasswordHash"", ""Role"", ""CreatedAt"", ""UpdatedAt"")
                            VALUES (@f, @l, @e, @pw, 'Admin', @dt, @dt) RETURNING ""Id"";";
                await using var adminCmd = new NpgsqlCommand(adminSql, conn);
                adminCmd.Parameters.AddWithValue("f", "İpek");
                adminCmd.Parameters.AddWithValue("l", "Bayrak");
                adminCmd.Parameters.AddWithValue("e", "ipek@bambicim.com");
                adminCmd.Parameters.AddWithValue("pw", adminPassword);
                adminCmd.Parameters.AddWithValue("dt", DateTime.UtcNow);
                var adminId = (int)(await adminCmd.ExecuteScalarAsync() ?? 0);
                users.Add((adminId, "ipek@bambicim.com"));
                
                for (int i = 0; i < 100; i++)
                {
                    var email = faker.Internet.Email().ToLower();
                    var sql = @"INSERT INTO ""Users"" (""FirstName"", ""LastName"", ""Email"", ""PasswordHash"", ""Role"", ""CreatedAt"", ""UpdatedAt"")
                                VALUES (@f, @l, @e, @pw, 'Customer', @dt, @dt) RETURNING ""Id"";";
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("f", faker.Name.FirstName());
                    cmd.Parameters.AddWithValue("l", faker.Name.LastName());
                    cmd.Parameters.AddWithValue("e", email);
                    cmd.Parameters.AddWithValue("pw", defaultPassword);
                    cmd.Parameters.AddWithValue("dt", DateTime.UtcNow);
                    var id = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                    users.Add((id, email));
                }
                Console.WriteLine($"Seeded {users.Count} users into userdb.");
            }

            // 2. catalogdb - Categories & Products
            await using (var conn = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=catalogdb;Username=postgres;Password=postgres"))
            {
                await conn.OpenAsync();
                await using (var cleanCmd = new NpgsqlCommand("TRUNCATE TABLE \"Categories\", \"Products\" CASCADE;", conn)) await cleanCmd.ExecuteNonQueryAsync();

                var faker = new Faker("en");
                
                for(int i = 0; i < 10; i++)
                {
                    var name = faker.Commerce.Categories(1)[0];
                    var slug = name.ToLower().Replace(" ", "-") + "-" + i;
                    var sql = @"INSERT INTO ""Categories"" (""Name"", ""Slug"", ""IsActive"") VALUES (@n, @s, true) RETURNING ""Id"";";
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("n", name);
                    cmd.Parameters.AddWithValue("s", slug);
                    var id = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                    categories.Add(id);
                }

                for (int i = 0; i < 100; i++)
                {
                    var price = Math.Round(faker.Random.Decimal(10.0m, 500.0m), 2);
                    var sku = faker.Commerce.Ean8();
                    var name = faker.Commerce.ProductName();
                    var catId = faker.PickRandom(categories);
                    
                    var sql = @"INSERT INTO ""Products"" (""Name"", ""Description"", ""Price"", ""Sku"", ""ImageUrl"", ""Stock"", ""IsActive"", ""CategoryId"", ""CreatedAt"", ""UpdatedAt"")
                                VALUES (@n, @d, @p, @sku, @i, @s, true, @cid, @dt, @dt) RETURNING ""Id"";";
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("n", name);
                    cmd.Parameters.AddWithValue("d", faker.Commerce.ProductDescription());
                    cmd.Parameters.AddWithValue("p", price);
                    cmd.Parameters.AddWithValue("sku", sku);
                    cmd.Parameters.AddWithValue("i", faker.Image.PicsumUrl());
                    cmd.Parameters.AddWithValue("s", faker.Random.Int(10, 500));
                    cmd.Parameters.AddWithValue("cid", catId);
                    cmd.Parameters.AddWithValue("dt", DateTime.UtcNow);
                    
                    var id = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                    products.Add((id, price, sku, name));
                }
                Console.WriteLine($"Seeded {categories.Count} categories and {products.Count} products into catalogdb.");
            }

            // 3. orderdb - Orders & OrderItems
            await using (var conn = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=orderdb;Username=postgres;Password=postgres"))
            {
                await conn.OpenAsync();
                await using (var cleanCmd = new NpgsqlCommand("TRUNCATE TABLE \"Orders\", \"OrderItems\" CASCADE;", conn)) await cleanCmd.ExecuteNonQueryAsync();

                var faker = new Faker("en");
                var methods = new[] { "pm_card_visa", "pm_card_chargeDeclined", "pm_card_chargeDeclinedInsufficientFunds", "pm_card_authenticationRequired" };
                var successStatuses = new[] { "Paid", "Shipped", "Delivered", "Completed" };
                var failStatuses = new[] { "Pending", "Cancelled" };
                
                for (int i = 0; i < 100; i++)
                {
                    var user = faker.PickRandom(users);
                    var prod = faker.PickRandom(products);
                    var amount = prod.Price;
                    
                    var method = faker.PickRandom(methods);
                    var succeeded = method == "pm_card_visa";
                    var status = succeeded ? faker.PickRandom(successStatuses) : faker.PickRandom(failStatuses);
                    var errorMsg = succeeded ? null : $"Mock decline for {method}";
                    
                    var sqlOrder = @"INSERT INTO ""Orders"" (""CustomerEmail"", ""TotalAmount"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
                                     VALUES (@c, @t, @st, @dt, @dt) RETURNING ""Id"";";
                    await using var cmdOrder = new NpgsqlCommand(sqlOrder, conn);
                    cmdOrder.Parameters.AddWithValue("c", user.Email);
                    cmdOrder.Parameters.AddWithValue("t", amount);
                    cmdOrder.Parameters.AddWithValue("st", status);
                    cmdOrder.Parameters.AddWithValue("dt", DateTime.UtcNow);
                    var orderId = (int)(await cmdOrder.ExecuteScalarAsync() ?? 0);
                    
                    orderData.Add((orderId, amount, method, succeeded, errorMsg));

                    var sqlItem = @"INSERT INTO ""OrderItems"" (""OrderId"", ""ProductId"", ""ProductName"", ""ProductSku"", ""UnitPrice"", ""Quantity"", ""LineTotal"")
                                    VALUES (@oid, @pid, @pn, @psku, @up, 1, @lt);";
                    await using var cmdItem = new NpgsqlCommand(sqlItem, conn);
                    cmdItem.Parameters.AddWithValue("oid", orderId);
                    cmdItem.Parameters.AddWithValue("pid", prod.Id);
                    cmdItem.Parameters.AddWithValue("pn", prod.Name);
                    cmdItem.Parameters.AddWithValue("psku", prod.Sku);
                    cmdItem.Parameters.AddWithValue("up", prod.Price);
                    cmdItem.Parameters.AddWithValue("lt", prod.Price);
                    await cmdItem.ExecuteNonQueryAsync();
                }
                Console.WriteLine($"Seeded {orderData.Count} orders with items into orderdb.");
            }

            // 4. paymentdb - Payments
            await using (var conn = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=paymentdb;Username=postgres;Password=postgres"))
            {
                await conn.OpenAsync();
                await using (var cleanCmd = new NpgsqlCommand("TRUNCATE TABLE \"Payments\" CASCADE;", conn)) await cleanCmd.ExecuteNonQueryAsync();

                var faker = new Faker("en");
                
                foreach (var data in orderData)
                {
                    var sql = @"INSERT INTO ""Payments"" (""OrderId"", ""TransactionId"", ""Amount"", ""Method"", ""Succeeded"", ""ErrorMessage"", ""ProcessedAt"")
                                VALUES (@o, @t, @a, @m, @s, @err, @dt);";
                                
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("o", data.OrderId);
                    cmd.Parameters.AddWithValue("t", "tx_" + faker.Random.AlphaNumeric(10));
                    cmd.Parameters.AddWithValue("a", data.Amount);
                    cmd.Parameters.AddWithValue("m", data.Method);
                    cmd.Parameters.AddWithValue("s", data.Succeeded);
                    
                    if (data.ErrorMsg == null)
                        cmd.Parameters.AddWithValue("err", DBNull.Value);
                    else
                        cmd.Parameters.AddWithValue("err", data.ErrorMsg);
                        
                    cmd.Parameters.AddWithValue("dt", DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync();
                }
                Console.WriteLine($"Seeded {orderData.Count} payments into paymentdb.");
            }

            Console.WriteLine("Successfully finished database seeding!");
        }
    }
}
