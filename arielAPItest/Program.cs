using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            // Load list of product IDs from JSON file
            var productIds = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("ItemList.json"));
            var credentials = JsonConvert.DeserializeObject<Secrets>(File.ReadAllText("secret.json"));

            // Load database connection string
            string connStr = $"Server={credentials.DbServer};Database={credentials.DbName};User Id={credentials.DbUser};Password={credentials.DbPassword};";

            // Create the API client once
            var client = new InventoryServiceClient();

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

                    // Validate response
                    if (response?.Inventory == null || response.Inventory.PartInventoryArray == null)
                    {
                        Console.WriteLine($"No inventory data found for Product ID: {productId}");
                        continue;
                    }

                    // comment out the next line if you don't want to see the raw response
                    Console.WriteLine($"------------------------------------------\nRetrieved inventory for Product: {response.Inventory.productId}");

                    foreach (var part in response.Inventory.PartInventoryArray)
                    {
                        string normalizedId = Program.NormalizePartId(part.partId);
                        // comment out the next line if you don't want to see the raw response
                        //Console.WriteLine($" - Part ID: {part.partId}");
                        //Console.WriteLine($" - Normalize: {normalizedId}");
                        //Console.WriteLine($"   Qty: {part.quantityAvailable.Quantity.value}");


                        using (SqlConnection conn = new SqlConnection(connStr))
                        {
                            conn.Open();

                            // test connection(checked)
                            //using (SqlCommand testCmd = new SqlCommand("SELECT 1", conn))
                            //{
                            //    testCmd.ExecuteScalar();
                            //}
                            //Console.WriteLine(" SQL Server connection test passed.");


                            SqlCommand checkCmd = new SqlCommand(@"
                                SELECT TOP 1 VendorItemID 
                                FROM ItemList 
                                WHERE VendorItemID LIKE @NormalizedId + '%'", conn);

                            checkCmd.Parameters.AddWithValue("@NormalizedId", normalizedId);

                            object result = checkCmd.ExecuteScalar();

                            if (result != null)
                            {
                                Console.WriteLine($"Found: {result}");
                            }
                            else
                            {
                                Console.WriteLine($"Not Found: {normalizedId}");
                            }

                        }
                        
                    }

                    // Optional delay to avoid overwhelming the API
                    Thread.Sleep(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error for Product ID {productId}: {ex.Message}");
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
        // Remove spaces and take only the first two dashes (e.g. AC102 - 01 -> AC102-01)
        var cleaned = partId.Replace(" ", "");
        var parts = cleaned.Split('-');

        if (parts.Length >= 2)
        {
            return $"{parts[0]}-{parts[1]}";  // e.g., AC102-01
        }

        return cleaned; // fallback
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

