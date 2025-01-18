using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using log4net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using TransactionApi.Models;
using Newtonsoft.Json;

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly string logFilePath = "Logs/logs.txt";
    private readonly Dictionary<string, string> allowedPartners = new()
    {
        { "FAKEGOOGLE", "FAKEPASSWORD1234" },
        { "FAKEPEOPLE", "FAKEPASSWORD4578" }
    };
    private static readonly ILog log = LogManager.GetLogger(typeof(TransactionController));

    [HttpGet]
    public IActionResult Welcome()
    {
        return Ok(new { Message = "Welcome to the Transaction API!" });
    }

    [HttpPost("submittrxmessage")]
    public IActionResult SubmitTransaction([FromBody] TransactionRequest request)
    {
        try
        {
            log.Info("SubmitTransaction method called."); // Log method entry
            var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
            log.Info($"Request JSON: {requestJson}"); // Log the entire request JSON

            var response = new TransactionResponse();

            // Validate request
            var validationResult = ValidateRequest(request);
            if (validationResult != null)
            {
                log.Warn($"Validation failed: {validationResult}");
                return BadRequest(validationResult);
            }

            // Calculate total amount from items
            long calculatedTotalAmount = request.Items.Sum(item => item.UnitPrice * item.Qty);
            if (calculatedTotalAmount != request.TotalAmount)
            {
                response.Result = 0;
                response.ResultMessage = "Invalid Total Amount.";
                LogRequestResponse(request, response);
                return BadRequest(response);
            }

            // Calculate discounts
            long totalDiscount = CalculateDiscount(request.TotalAmount);
            response.TotalAmount = request.TotalAmount;
            response.TotalDiscount = totalDiscount;
            response.FinalAmount = request.TotalAmount - totalDiscount;
            response.Result = 1;

            // Log request and response
            LogRequestResponse(request, response);

            return Ok(response);
        }
        catch (Exception ex)
        {
            log.Error($"Error processing request: {ex.Message}", ex);
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpGet("test-signature")]
    public IActionResult TestSignature()
    {
        // Create a sample transaction request
        var request = new TransactionRequest
        {
            PartnerKey = "FAKEGOOGLE",
            PartnerRefNo = "FG-00001",
            TotalAmount = 100000, // Total amount in cents (1000 MYR)
            Timestamp = "2025-01-17T19:17:26.0000000Z", // Example UTC timestamp
            Items = new List<ItemDetail>
            {
                new ItemDetail { PartnerItemRef = "i-00001", Name = "Pen", Qty = 4, UnitPrice = 20000 },
                new ItemDetail { PartnerItemRef = "i-00002", Name = "Ruler", Qty = 2, UnitPrice = 10000 }
            }
        };

        // Generate the signature
        string generatedSignature = GenerateSignature(request);

        // Log the generated signature
        log.Info($"Generated Signature: {generatedSignature}");

        // Return the generated signature as a response
        return Ok(new { Signature = generatedSignature });
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginModel login)
    {
        log.Info($"Login attempt for user: {login.Username} with password: {login.Password}"); // Log the login attempt
        try
        {
            // Validate the user credentials (this is just an example)
            if (login.Username == "test" && login.Password == "password")
            {
                // Create a transaction request for signature generation
                var request = new TransactionRequest
                {
                    PartnerKey = "FAKEGOOGLE",
                    PartnerRefNo = "FG-00001",
                    TotalAmount = 100000, // Example amount in cents
                    Timestamp = "2025-01-18T18:22:20Z", // Use the provided current local time
                    Items = new List<ItemDetail>() // Populate as needed
                };

                // Generate the signature
                string generatedSignature = GenerateSignature(request);

                // Return the generated signature
                return Ok(new { Sig = generatedSignature });
            }

            log.Info($"Failed login for user: {login.Username} with password: {login.Password}"); // Log failed login
            return Unauthorized();
        }
        catch (Exception ex)
        {
            log.Error($"Error occurred during login for user {login.Username}: {ex.Message}", ex); // Log the error details
            log.Info($"Login attempt for user: {login.Username} failed with error: {ex.Message}"); // Log the error
            Console.WriteLine($"Error occurred during login for user {login.Username}: {ex.Message}"); // Fallback logging
            return StatusCode(500, "Internal server error. Please try again later.");
        }
    }

    // [HttpPost("generate-signature")]
    // public IActionResult GenerateSignatureEndpoint([FromBody] TransactionRequest request)
    // {
    //     // Validate the request parameters
    //     if (string.IsNullOrEmpty(request.PartnerKey) || 
    //         string.IsNullOrEmpty(request.PartnerRefNo) || 
    //         request.TotalAmount <= 0 || 
    //         string.IsNullOrEmpty(request.PartnerPassword) || 
    //         request.Items == null || !request.Items.Any())
    //     {
    //         return BadRequest("Invalid input parameters.");
    //     }
    // 
    //     // Generate the signature using the private method
    //     string generatedSignature = GenerateSignature(request);
    // 
    //     return Ok(new { Signature = generatedSignature });
    // }

    private string ValidateRequest(TransactionRequest request)
    {
        if (string.IsNullOrEmpty(request.PartnerKey) || !allowedPartners.ContainsKey(request.PartnerKey))
        {
            log.Info("Validation failed: Access Denied!");
            return "Access Denied!";
        }

        if (string.IsNullOrEmpty(request.PartnerRefNo))
        {
            log.Info("Validation failed: partnerrefno is required.");
            return "partnerrefno is required.";
        }

        if (string.IsNullOrEmpty(request.PartnerPassword))
        {
            log.Info("Validation failed: partnerpassword is required.");
            return "partnerpassword is required.";
        }

        if (request.TotalAmount <= 0)
        {
            log.Info("Validation failed: Invalid Total Amount.");
            return "Invalid Total Amount.";
        }

        if (request.Items == null || !request.Items.Any())
        {
            log.Info("Validation failed: Items are required.");
            return "Items are required.";
        }

        foreach (var item in request.Items)
        {
            if (string.IsNullOrEmpty(item.Name))
            {
                log.Info("Validation failed: Item name is required.");
                return "Item name is required.";
            }
            if (item.Qty <= 1 || item.Qty > 5)
            {
                log.Info("Validation failed: Quantity must be > 1 and <= 5.");
                return "Quantity must be > 1 and <= 5.";
            }
            if (item.UnitPrice <= 0)
            {
                log.Info("Validation failed: Unit price must be positive.");
                return "Unit price must be positive.";
            }
        }

        try
        {
            log.Info("Test log entry: Logging is working.");
            log.Info($"Incoming Timestamp: {request.Timestamp}");
            if (!DateTime.TryParse(request.Timestamp, out DateTime timestamp) || 
                Math.Abs((DateTime.UtcNow.AddHours(8) - timestamp).TotalMinutes) > 60) // Allow for a 60-minute window
            {
                log.Info("Validation failed: Timestamp expired.");
                return $"Time Expired. Current UTC: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}";
            }

            // Log the current UTC time and the parsed timestamp for debugging
            DateTime currentUtcTime = DateTime.UtcNow;
            log.Info($"Current UTC Time: {currentUtcTime}");
            log.Info($"Parsed Timestamp: {timestamp}");
            log.Info($"Difference in Minutes: {Math.Abs((currentUtcTime.AddHours(8) - timestamp).TotalMinutes)}");
        }
        catch (Exception ex)
        {
            log.Error($"Error occurred during logging: {ex.Message}", ex); // Log the error details
            log.Info($"Logging failed with error: {ex.Message}"); // Log the error
            Console.WriteLine($"Error occurred during logging: {ex.Message}"); // Fallback logging
            return "Internal Server Error";
        }

        string generatedSignature = GenerateSignature(request);
        if (generatedSignature != request.Sig)
        {
            log.Info("Validation failed: Signature Mismatch.");
            return "Signature Mismatch.";
        }

        return null; // No validation errors
    }

    private string GenerateSignature(TransactionRequest request)
    {
        // Use the provided current local time
        string formattedTimestamp = "20250118162534"; // Represents 2025-01-18T16:25:34 in the format yyyyMMddHHmmss

        // Get the partner password
        string partnerPassword = allowedPartners[request.PartnerKey];

        // Concatenate parameters in the desired format
        string dataToSign = $"{formattedTimestamp}{request.PartnerKey}{request.PartnerRefNo}{request.TotalAmount}{partnerPassword}";

        // Generate SHA256 hash
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
            string base64Hash = Convert.ToBase64String(hash);

            // Return the formatted signature
            return $"{formattedTimestamp}{request.PartnerKey}{request.PartnerRefNo}{request.TotalAmount}{base64Hash}";
        }
    }

    private long CalculateDiscount(long totalAmount)
    {
        double discount = 0;

        // Base Discount
        if (totalAmount < 20000)
            discount = 0;
        else if (totalAmount <= 50000)
            discount = totalAmount * 0.05;
        else if (totalAmount <= 80000)
            discount = totalAmount * 0.07;
        else if (totalAmount <= 120000)
            discount = totalAmount * 0.10;
        else
            discount = totalAmount * 0.15;

        // Conditional Discounts
        if (IsPrime(totalAmount) && totalAmount > 50000)
            discount += totalAmount * 0.08;

        if (totalAmount % 10 == 5 && totalAmount > 90000)
            discount += totalAmount * 0.10;

        // Cap on Maximum Discount
        if (discount > totalAmount * 0.20)
            discount = totalAmount * 0.20;

        return (long)discount;
    }

    private bool IsPrime(long number)
    {
        if (number <= 1) return false;
        if (number == 2) return true;
        if (number % 2 == 0) return false;

        for (long i = 3; i <= Math.Sqrt(number); i += 2)
        {
            if (number % i == 0) return false;
        }
        return true;
    }

    private void LogRequestResponse(TransactionRequest request, TransactionResponse response)
    {
        var logEntry = $"Request: {System.Text.Json.JsonSerializer.Serialize(request)}\n" +
                       $"Response: {System.Text.Json.JsonSerializer.Serialize(response)}\n\n";
        log.Info(logEntry); // Log the request and response
    }

    private string GenerateTransactionRequestJson()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); // Generate current UTC timestamp
        log.Info($"Timestamp: {timestamp}"); // Log the generated timestamp
        var request = new
        {
            partnerkey = "FAKEGOOGLE",
            partnerrefno = "FG-00001",
            partnerpassword = "RkFLRVBBU1NXT1JEMTIzNA==",
            totalamount = 100000,
            items = new[]
            {
                new { partneritemref = "i-00001", name = "Pen", qty = 4, unitprice = 20000 },
                new { partneritemref = "i-00002", name = "Ruler", qty = 2, unitprice = 10000 }
            },
            timestamp,
            sig = GenerateSignature(new TransactionRequest
            {
                PartnerKey = "FAKEGOOGLE",
                PartnerRefNo = "FG-00001",
                TotalAmount = 100000,
                Timestamp = timestamp,
                Items = new List<ItemDetail>
                {
                    new ItemDetail { PartnerItemRef = "i-00001", Name = "Pen", Qty = 4, UnitPrice = 20000 },
                    new ItemDetail { PartnerItemRef = "i-00002", Name = "Ruler", Qty = 2, UnitPrice = 10000 }
                }
            }) // Call to method to generate the signature
        };
        log.Info($"Signature: {request.sig}"); // Log the generated signature

        return JsonConvert.SerializeObject(request);
    }

    public void TestSignatureGeneration()
    {
        // Sample input values
        string timestamp = "2025-01-18T04:39:38Z"; // Example timestamp
        string partnerKey = "FAKEGOOGLE";
        string partnerRefNo = "FG-00001";
        long totalAmount = 100000; // Example total amount
        string partnerPassword = "FAKEPASSWORD1234"; // Example password

        // Generate the signature using your method
        string generatedSignature = GenerateSignature(new TransactionRequest
        {
            PartnerKey = partnerKey,
            PartnerRefNo = partnerRefNo,
            TotalAmount = totalAmount,
            Timestamp = timestamp,
            Items = new List<ItemDetail>
            {
                new ItemDetail { PartnerItemRef = "i-00001", Name = "Pen", Qty = 4, UnitPrice = 20000 },
                new ItemDetail { PartnerItemRef = "i-00002", Name = "Ruler", Qty = 2, UnitPrice = 10000 }
            }
        });

        // Expected signature (calculate this separately or hardcode it for testing)
        string expectedSignature = "EXPECTED_SIGNATURE_HERE"; // Replace with the actual expected signature

        // Log the results
        log.Info($"Generated Signature: {generatedSignature}");
        log.Info($"Expected Signature: {expectedSignature}");

        // Check if they match
        if (generatedSignature == expectedSignature)
        {
            log.Info("Signature generation works correctly.");
        }
        else
        {
            log.Info("Signature generation failed. Check the implementation.");
        }
    }

    public void TestLoginSignature(string receivedSignature, string timestamp, string partnerKey, string partnerRefNo, long totalAmount, string partnerPassword)
    {
        // Generate the signature using the same method
        string generatedSignature = GenerateSignature(new TransactionRequest
        {
            PartnerKey = partnerKey,
            PartnerRefNo = partnerRefNo,
            TotalAmount = totalAmount,
            Timestamp = timestamp,
            Items = new List<ItemDetail>
            {
                new ItemDetail { PartnerItemRef = "i-00001", Name = "Pen", Qty = 4, UnitPrice = 20000 },
                new ItemDetail { PartnerItemRef = "i-00002", Name = "Ruler", Qty = 2, UnitPrice = 10000 }
            }
        });

        // Log the results
        log.Info($"Received Signature: {receivedSignature}");
        log.Info($"Generated Signature: {generatedSignature}");

        // Check if they match
        if (generatedSignature == receivedSignature)
        {
            log.Info("Signature is valid.");
        }
        else
        {
            log.Info("Signature is invalid. Check the implementation.");
        }
    }
}
