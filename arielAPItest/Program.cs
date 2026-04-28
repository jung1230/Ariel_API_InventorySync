using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Threading;
using System.Linq;
class Program
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Current directory: " + Directory.GetCurrentDirectory());
            //Console.WriteLine("ItemList path: " + Path.GetFullPath("ItemList.json"));
            // Load both JSON files
            var credentials = JsonConvert.DeserializeObject<Secrets>(File.ReadAllText("secret.json"));

            // Load database connection string
            string connStr = $"Server={credentials.DbServer};Database={credentials.DbName};User Id={credentials.DbUser};Password={credentials.DbPassword};";


            var productIds = new HashSet<string>();
            var vendorItemMap = new Dictionary<string, string>();


            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(@"
                    SELECT il.VendorItemID, il.ItemID
                    FROM Ariel.dbo.ItemList AS il
                    JOIN Ariel.dbo.ItemVendorInfo AS ivi
                        ON il.ItemID = ivi.ItemID
                    WHERE ivi.vendorID = @vendorID AND il.Active = 1", conn);

                cmd.Parameters.AddWithValue("@vendorID", "ACH");


                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var vendorItemId = reader["VendorItemID"].ToString();

                        if (string.IsNullOrWhiteSpace(vendorItemId))
                            continue;

                        vendorItemMap[vendorItemId.Trim()] = reader["ItemID"].ToString();


                        // normalize like your example
                        var cleaned = vendorItemId.Replace("  ", " ").Trim();
                        var parts = cleaned.Split('-');

                        if (parts.Length >= 1)
                        {
                            var baseId = parts[0].Trim();
                            productIds.Add(baseId); // HashSet ensures uniqueness
                        }
                    }
                }
            }

            // -------------------- add items here for exceptions! -------------------- 
            productIds.Add("ACG103 GLASS - Clear");
            // -------------------- add items here for exceptions! -------------------- 

            Console.WriteLine("New Item List: " + string.Join(", ", productIds));
            // Create the API client
            var client = new InventoryServiceClient();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                foreach (var productId in productIds)
                {
                    try
                    {
                        // Build the API request
                        var request = new GetInventoryLevelsRequest
                        {
                            wsVersion = wsVersion.Item200,
                            id = credentials.APISenderId,
                            password = credentials.APIPassword,
                            productId = productId
                        };

                        // Make the API call
                        var response = client.getInventoryLevels(request);

                        if (response?.Inventory == null || response.Inventory.PartInventoryArray == null)
                        {
                            Console.WriteLine($"------------------------------------------\nNo inventory data found for Product ID from promostandards: {productId}");
                            continue;
                        }

                        //Console.WriteLine($"------------------------------------------\nRetrieved inventory for Product from promostandards: {response.Inventory.productId}");

                        foreach (var part in response.Inventory.PartInventoryArray)
                        {
                            string normalizedId = Program.NormalizePartId(part.partId);
                            // comment out the next line if you don't want to see the raw response
                            //Console.WriteLine($"**************************\nPart ID: {part.partId}");
                            //Console.WriteLine($" - Normalize: {normalizedId}");
                            //Console.WriteLine($"Qty: {part.quantityAvailable.Quantity.value}");

                            var matched = vendorItemMap.FirstOrDefault(x => x.Key.StartsWith(normalizedId));

                            if (!string.IsNullOrEmpty(matched.Key))
                            {
                                //Console.WriteLine($"Found in Ariel DB: {VendorItemID}");

                                var invoiceNo = "DWA-INV";
                                //update the quantity of the item in the database
                                SqlCommand updateQuantity = new SqlCommand(@"
                                        UPDATE PurchReceiptDetail 
                                        SET RecQuantity = @Quantity 
                                        WHERE ItemID = @ItemID and InvoiceNo = @InvoiceNo", conn);
                                updateQuantity.Parameters.AddWithValue("@Quantity", part.quantityAvailable.Quantity.value);
                                updateQuantity.Parameters.AddWithValue("@ItemID", matched.Value);
                                updateQuantity.Parameters.AddWithValue("@InvoiceNo", invoiceNo);
                                int rowsAffected = updateQuantity.ExecuteNonQuery();
                                if (rowsAffected > 0)
                                {
                                    Console.WriteLine($"Updated quantity for VendorItemID: {matched.Key} to {part.quantityAvailable.Quantity.value}");
                                }
                                else
                                {
                                    Console.WriteLine($"No rows updated for VendorItemID: {matched.Key}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Not Found in Ariel DB: {normalizedId}");
                            }
                        }

                        // Optional delay to avoid overwhelming the API
                        //Thread.Sleep(200);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error for Product ID {productId}: {ex.Message}");
                    }
                }
            }

            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("General error: " + ex.Message);
        }
    }

    static string NormalizePartId(string partId)
    {
        var cleaned = partId.Replace("  ", " ");
        var parts = cleaned.Split('-');

        if (parts.Length >= 2)
        {
            var part = parts[0];
            return $"{parts[0]}-{parts[1]}";
        }

        return cleaned;
    }
}

public class Secrets
{
    public string APISenderId { get; set; }
    public string APIPassword { get; set; }

    public string DbServer { get; set; }
    public string DbName { get; set; }
    public string DbUser { get; set; }
    public string DbPassword { get; set; }
}
