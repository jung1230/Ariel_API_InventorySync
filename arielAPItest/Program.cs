using System;
using System.Configuration; // For future DB connection

class Program
{
    static void Main(string[] args)
    {
        try
        {
            // Create the client
            var client = new InventoryServiceClient();

            // Build the request
            var request = new GetInventoryLevelsRequest
            {
                wsVersion = wsVersion.Item200,
                id = "Ariel",      
                password = "Ariel6888",  
                productId = "AC231"       
                // You can add filters here if needed
            };

            // Call the service
            var response = client.getInventoryLevels(request);

            // Print results
            Console.WriteLine("Product ID: " + response.Inventory.productId);

            foreach (var part in response.Inventory.PartInventoryArray)
            {
                Console.WriteLine($" - Part ID: {part.partId}");
                Console.WriteLine($"   Qty: {part.quantityAvailable.Quantity.value} {part.quantityAvailable.Quantity.uom}");
                // Console.WriteLine($"   Last Modified: {part.lastModified}");
            }


            string connStr = ConfigurationManager.ConnectionStrings["InventoryDb"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                // your INSERT / UPDATE logic
            }


            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error calling API: " + ex.Message);
        }
    }
}
