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
            var defaultPassword = "$2a$11$wK1W2/uQ51LhO0z9HlD.1ulm7C82rG8M9BfX97H9mKzP.0m2I6m1C"; // Password123!

            var users = new List<(int Id, string Email)>();
            var categories = new List<int>();
            var products = new List<(int Id, decimal Price, string Sku, string Name)>();
            var orders = new List<(int Id, decimal TotalAmount)>();

            // 1. userdb - Users
            await using (var conn = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=userdb;Username=postgres;Password=postgres"))
            {
                await conn.OpenAsync();
                await using (var cleanCmd = new NpgsqlCommand("TRUNCATE TABLE \"Users\" CASCADE;", conn)) await cleanCmd.ExecuteNonQueryAsync();

                var faker = new Faker("en");
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
                    var id = (int)(await cmd.ExecuteScalarAsync());
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
                    var id = (int)(await cmd.ExecuteScalarAsync());
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
                    
                    var id = (int)(await cmd.ExecuteScalarAsync());
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
                
                for (int i = 0; i < 100; i++)
                {
                    var user = faker.PickRandom(users);
                    var prod = faker.PickRandom(products);
                    var amount = prod.Price;
                    
                    var sqlOrder = @"INSERT INTO ""Orders"" (""CustomerEmail"", ""TotalAmount"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
                                     VALUES (@c, @t, 'Completed', @dt, @dt) RETURNING ""Id"";";
                    await using var cmdOrder = new NpgsqlCommand(sqlOrder, conn);
                    cmdOrder.Parameters.AddWithValue("c", user.Email);
                    cmdOrder.Parameters.AddWithValue("t", amount);
                    cmdOrder.Parameters.AddWithValue("dt", DateTime.UtcNow);
                    var orderId = (int)(await cmdOrder.ExecuteScalarAsync());
                    orders.Add((orderId, amount));

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
                Console.WriteLine($"Seeded {orders.Count} orders with items into orderdb.");
            }

            // 4. paymentdb - Payments
            await using (var conn = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=paymentdb;Username=postgres;Password=postgres"))
            {
                await conn.OpenAsync();
                await using (var cleanCmd = new NpgsqlCommand("TRUNCATE TABLE \"Payments\" CASCADE;", conn)) await cleanCmd.ExecuteNonQueryAsync();

                var faker = new Faker("en");
                
                foreach (var order in orders)
                {
                    var sql = @"INSERT INTO ""Payments"" (""OrderId"", ""TransactionId"", ""Amount"", ""Method"", ""Succeeded"", ""ProcessedAt"")
                                VALUES (@o, @t, @a, 'Card', true, @dt);";
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("o", order.Id);
                    cmd.Parameters.AddWithValue("t", "tx_" + faker.Random.AlphaNumeric(10));
                    cmd.Parameters.AddWithValue("a", order.TotalAmount);
                    cmd.Parameters.AddWithValue("dt", DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync();
                }
                Console.WriteLine($"Seeded {orders.Count} payments into paymentdb.");
            }

            Console.WriteLine("Successfully finished database seeding!");
        }
    }
}
