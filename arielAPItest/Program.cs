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
                        Console.WriteLine($"------------------------------------------\nNo inventory data found for Product ID: {productId}");
                        continue;
                    }

                    // comment out the next line if you don't want to see the raw response
                    Console.WriteLine($"------------------------------------------\nRetrieved inventory for Product: {response.Inventory.productId}");


                    using (SqlConnection conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        foreach (var part in response.Inventory.PartInventoryArray)
                        {
                            string normalizedId = Program.NormalizePartId(part.partId);
                            // comment out the next line if you don't want to see the raw response
                            Console.WriteLine($"**************************\nPart ID: {part.partId}");
                            //Console.WriteLine($" - Normalize: {normalizedId}");
                            Console.WriteLine($"Qty: {part.quantityAvailable.Quantity.value}");


                            // get the whole VendorItemID based on normalized partId
                            SqlCommand checkVendorItemID = new SqlCommand(@"
                                    SELECT TOP 1 VendorItemID 
                                    FROM ItemList 
                                    WHERE VendorItemID LIKE @NormalizedId + '%'", conn);

                            checkVendorItemID.Parameters.AddWithValue("@NormalizedId", normalizedId);

                            object VendorItemID = checkVendorItemID.ExecuteScalar();

                            if (VendorItemID != null)
                            {
                                //Console.WriteLine($"Found in Ariel DB: {VendorItemID}");


                                // retrieve ItemID based on VendorItemID
                                SqlCommand getItemID = new SqlCommand(@"
                                        SELECT TOP 1 ItemID 
                                        FROM ItemList 
                                        WHERE VendorItemID = @VendorItemID", conn);
                                getItemID.Parameters.AddWithValue("@VendorItemID", VendorItemID);
                                object ItemID = getItemID.ExecuteScalar();
                                //Console.WriteLine($"ItemID: {ItemID}");


                                //update the quantity of the item in the database
                                SqlCommand updateQuantity = new SqlCommand(@"
                                        UPDATE PurchReceiptDetail 
                                        SET RecQuantity = @Quantity 
                                        WHERE ItemID = @ItemID", conn);
                                updateQuantity.Parameters.AddWithValue("@Quantity", part.quantityAvailable.Quantity.value);
                                updateQuantity.Parameters.AddWithValue("@ItemID", ItemID);
                                int rowsAffected = updateQuantity.ExecuteNonQuery();
                                if (rowsAffected > 0)
                                {
                                    Console.WriteLine($"Updated quantity for ItemID: {ItemID} to {part.quantityAvailable.Quantity.value}");
                                }
                                else
                                {
                                    Console.WriteLine($"No rows updated for ItemID: {ItemID}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Not Found in Ariel DB: {normalizedId}");
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

