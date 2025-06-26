# PromoStandards Inventory Sync Tool

This tool automates the retrieval of inventory data from a supplier's PromoStandards Inventory 2.0.0 SOAP API and optionally syncs it with a local SQL Server database.

It is written in **C# (.NET Framework)** and designed to be run on a Windows machine via **Task Scheduler**.

---

## Project Structure
After clicking **Start** button in vsstudio, the project will create a **bin** folder containing the following files:
Debug/
??? ArielAPItest.exe        # Compiled console application
??? Newtonsoft.Json.dll     # JSON parsing dependency
??? ArielAPItest.exe.config # Optional config file

Make sure to copy secret.json and ItemList.json into the same directory as ArielAPItest.exe.

## secret.json
Create a secret.json file in the same folder as the executable, with the following structure:

```json
{
  "APISenderId": "your_api_id",
  "APIPassword": "your_api_password",
  "DbServer": "your_sql_server",
  "DbName": "your_database",
  "DbUser": "your_username",
  "DbPassword": "your_password"
}

## ItemList.json
This file should contain a list of product IDs to request inventory data for.


## Deployment Steps
1. Copy all required files to a folder (e.g., C:\API\ArielAPItest)
2. Test manually via command line
3. Create a Windows Task Scheduler job:
	Program/script: ArielAPItest.exe
	Start in: C:\API\ArielAPItest
	Trigger: run daily or hourly as needed

## Contact
If you have any questions or need assistance, feel free to reach out:
Email: [alanchen20072@gmail.com]