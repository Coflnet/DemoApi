using Coflnet.GoogleSheets;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Coflnet.Controllers;

/// <summary>
/// Controller for Google Sheets operations including CSV upload
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GoogleSheetsController : ControllerBase
{
    private readonly GoogleCredentialProvider credentialProvider;
    private readonly ILogger<GoogleSheetsController> logger;
    private readonly IConfiguration configuration;

    public GoogleSheetsController(
        GoogleCredentialProvider credentialProvider,
        ILogger<GoogleSheetsController> logger,
        IConfiguration configuration)
    {
        this.credentialProvider = credentialProvider;
        this.logger = logger;
        this.configuration = configuration;
    }

    /// <summary>
    /// Uploads CSV data to a Google Sheets document
    /// </summary>
    /// <param name="request">Upload request containing spreadsheet details and CSV data</param>
    /// <returns>Result of the upload operation</returns>
    [HttpPost("upload-csv")]
    public async Task<ActionResult<SheetUpdateResult>> UploadCsvToSheet([FromBody] CsvUploadRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SpreadsheetId))
            {
                return BadRequest("SpreadsheetId is required");
            }

            if (string.IsNullOrWhiteSpace(request.CsvData))
            {
                return BadRequest("CsvData is required");
            }

            if (string.IsNullOrWhiteSpace(request.SheetName))
            {
                request.SheetName = "Sheet1";
            }

            // Get credentials based on configuration
            var credential = await GetCredentialAsync(request.AuthOptions);
            
            // Create Google Sheets service
            var serviceFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var sheetsServiceLogger = serviceFactory.CreateLogger<GoogleSheetsService>();
            using var sheetsService = new GoogleSheetsService(credential, sheetsServiceLogger);

            // Upload CSV data
            var result = await sheetsService.UploadCsvToSheetAsync(
                request.SpreadsheetId,
                request.SheetName,
                request.CsvData,
                request.HasHeaders,
                request.ClearExistingData);

            if (result.Success)
            {
                logger.LogInformation($"Successfully uploaded CSV to spreadsheet {request.SpreadsheetId}, sheet {request.SheetName}");
                return Ok(result);
            }
            else
            {
                logger.LogError($"Failed to upload CSV: {result.Error}");
                return BadRequest(result);
            }
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid request parameters");
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Authentication failed");
            return Unauthorized("Authentication failed. Please check your credentials.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading CSV to Google Sheets");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new Google Sheets spreadsheet and populates it with CSV data
    /// </summary>
    /// <param name="request">Creation request containing spreadsheet details and CSV data</param>
    /// <returns>Result of the creation operation</returns>
    [HttpPost("create-spreadsheet")]
    public async Task<ActionResult<SpreadsheetCreationResult>> CreateSpreadsheetFromCsv([FromBody] SpreadsheetCreationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Title is required");
            }

            if (string.IsNullOrWhiteSpace(request.CsvData))
            {
                return BadRequest("CsvData is required");
            }

            if (string.IsNullOrWhiteSpace(request.SheetName))
            {
                request.SheetName = "Sheet1";
            }

            // Get credentials based on configuration
            var credential = await GetCredentialAsync(request.AuthOptions);
            
            // Create Google Sheets service
            var serviceFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var sheetsServiceLogger = serviceFactory.CreateLogger<GoogleSheetsService>();
            using var sheetsService = new GoogleSheetsService(credential, sheetsServiceLogger);

            // Create spreadsheet with CSV data
            var result = await sheetsService.CreateSpreadsheetFromCsvAsync(
                request.Title,
                request.CsvData,
                request.SheetName,
                request.HasHeaders);

            if (result.Success)
            {
                logger.LogInformation($"Successfully created spreadsheet '{request.Title}' with ID: {result.SpreadsheetId}");
                return Ok(result);
            }
            else
            {
                logger.LogError($"Failed to create spreadsheet: {result.Error}");
                return BadRequest(result);
            }
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid request parameters");
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Authentication failed");
            return Unauthorized("Authentication failed. Please check your credentials.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Google Sheets spreadsheet");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets information about a Google Sheets spreadsheet
    /// </summary>
    /// <param name="spreadsheetId">The ID of the spreadsheet</param>
    /// <param name="authOptions">Authentication options (optional, uses default from config)</param>
    /// <returns>Spreadsheet information</returns>
    [HttpGet("spreadsheet/{spreadsheetId}")]
    public async Task<ActionResult<SpreadsheetInfo>> GetSpreadsheetInfo(
        string spreadsheetId, 
        [FromQuery] GoogleSheetsAuthOptions? authOptions = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                return BadRequest("SpreadsheetId is required");
            }

            // Get credentials based on configuration
            var credential = await GetCredentialAsync(authOptions);
            
            // Create Google Sheets service
            var serviceFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var sheetsServiceLogger = serviceFactory.CreateLogger<GoogleSheetsService>();
            using var sheetsService = new GoogleSheetsService(credential, sheetsServiceLogger);

            // Get spreadsheet information
            var result = await sheetsService.GetSpreadsheetInfoAsync(spreadsheetId);

            logger.LogInformation($"Retrieved information for spreadsheet: {spreadsheetId}");
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Authentication failed");
            return Unauthorized("Authentication failed. Please check your credentials.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting spreadsheet info for ID: {spreadsheetId}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Uploads Bundesliga match data as CSV to Google Sheets
    /// </summary>
    /// <param name="request">Request containing match data export options</param>
    /// <returns>Result of the upload operation</returns>
    [HttpPost("upload-bundesliga-data")]
    public async Task<ActionResult<SheetUpdateResult>> UploadBundesligaData([FromBody] BundesligaUploadRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SpreadsheetId))
            {
                return BadRequest("SpreadsheetId is required");
            }

            if (string.IsNullOrWhiteSpace(request.SheetName))
            {
                request.SheetName = "Bundesliga_Data";
            }

            // Get credentials
            var credential = await GetCredentialAsync(request.AuthOptions);
            
            // Create Google Sheets service
            var serviceFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var sheetsServiceLogger = serviceFactory.CreateLogger<GoogleSheetsService>();
            using var sheetsService = new GoogleSheetsService(credential, sheetsServiceLogger);

            // Generate CSV data based on request type
            string csvData;
            switch (request.DataType)
            {
                case BundesligaDataType.Matches:
                    csvData = await GenerateMatchesCsvAsync();
                    break;
                case BundesligaDataType.Events:
                    csvData = await GenerateEventsCsvAsync();
                    break;
                case BundesligaDataType.Goals:
                    csvData = await GenerateGoalsCsvAsync();
                    break;
                case BundesligaDataType.Cards:
                    csvData = await GenerateCardsCsvAsync();
                    break;
                case BundesligaDataType.Statistics:
                    csvData = await GenerateStatisticsCsvAsync();
                    break;
                default:
                    return BadRequest($"Unsupported data type: {request.DataType}");
            }

            // Upload CSV data
            var result = await sheetsService.UploadCsvToSheetAsync(
                request.SpreadsheetId,
                request.SheetName,
                csvData,
                hasHeaders: true,
                clearExistingData: request.ClearExistingData);

            if (result.Success)
            {
                logger.LogInformation($"Successfully uploaded Bundesliga {request.DataType} data to spreadsheet {request.SpreadsheetId}");
                return Ok(result);
            }
            else
            {
                logger.LogError($"Failed to upload Bundesliga data: {result.Error}");
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading Bundesliga data to Google Sheets");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    private async Task<Google.Apis.Auth.OAuth2.GoogleCredential> GetCredentialAsync(GoogleSheetsAuthOptions? authOptions)
    {
        // Use provided auth options or fall back to configuration
        var options = authOptions ?? GetDefaultAuthOptions();

        switch (options.Method)
        {
            case AuthMethod.ServiceAccount:
                if (!string.IsNullOrWhiteSpace(options.ServiceAccountJson))
                {
                    return credentialProvider.GetServiceAccountCredentialFromJson(options.ServiceAccountJson);
                }
                else if (!string.IsNullOrWhiteSpace(options.ServiceAccountKeyPath))
                {
                    return await credentialProvider.GetServiceAccountCredentialAsync(options.ServiceAccountKeyPath);
                }
                else
                {
                    throw new InvalidOperationException("Service account credentials not configured");
                }

            case AuthMethod.UserOAuth2:
                if (!string.IsNullOrWhiteSpace(options.ClientSecretsPath))
                {
                    var userCred = await credentialProvider.GetUserCredentialFromFileAsync(
                        options.ClientSecretsPath, 
                        options.UserId, 
                        options.DataStorePath);
                    return Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(userCred.Token.AccessToken);
                }
                else if (!string.IsNullOrWhiteSpace(options.ClientId) && !string.IsNullOrWhiteSpace(options.ClientSecret))
                {
                    var userCred = await credentialProvider.GetUserCredentialAsync(
                        options.ClientId, 
                        options.ClientSecret, 
                        options.UserId, 
                        options.DataStorePath);
                    return Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(userCred.Token.AccessToken);
                }
                else
                {
                    throw new InvalidOperationException("OAuth2 credentials not configured");
                }

            default:
                throw new InvalidOperationException($"Unsupported authentication method: {options.Method}");
        }
    }

    private GoogleSheetsAuthOptions GetDefaultAuthOptions()
    {
        var options = new GoogleSheetsAuthOptions();
        
        // Try to load from configuration
        configuration.GetSection("GoogleSheets:Auth").Bind(options);
        
        return options;
    }

    private Task<string> GenerateMatchesCsvAsync()
    {
        // This would integrate with your existing Bundesliga crawler
        // For now, return a sample structure
        var csv = new StringBuilder();
        csv.AppendLine("Date,Home Team,Away Team,Home Score,Away Score,Status,Matchday");
        csv.AppendLine("2024-12-20,Bayern München,Borussia Dortmund,3,1,Finished,15");
        csv.AppendLine("2024-12-20,RB Leipzig,Eintracht Frankfurt,2,0,Finished,15");
        return Task.FromResult(csv.ToString());
    }

    private Task<string> GenerateEventsCsvAsync()
    {
        var csv = new StringBuilder();
        csv.AppendLine("Match,Time,Event Type,Player,Team,Details");
        csv.AppendLine("Bayern vs Dortmund,23',Goal,Robert Lewandowski,Bayern München,Right foot shot from the center of the box");
        csv.AppendLine("Bayern vs Dortmund,45',Yellow Card,Mats Hummels,Borussia Dortmund,Unsporting behavior");
        return Task.FromResult(csv.ToString());
    }

    private Task<string> GenerateGoalsCsvAsync()
    {
        var csv = new StringBuilder();
        csv.AppendLine("Match,Time,Scorer,Team,Assist,Shot Type,Shot Placement");
        csv.AppendLine("Bayern vs Dortmund,23',Robert Lewandowski,Bayern München,Thomas Müller,Right foot,Center of goal");
        return Task.FromResult(csv.ToString());
    }

    private Task<string> GenerateCardsCsvAsync()
    {
        var csv = new StringBuilder();
        csv.AppendLine("Match,Time,Player,Team,Card Type,Reason");
        csv.AppendLine("Bayern vs Dortmund,45',Mats Hummels,Borussia Dortmund,Yellow,Unsporting behavior");
        return Task.FromResult(csv.ToString());
    }

    private Task<string> GenerateStatisticsCsvAsync()
    {
        var csv = new StringBuilder();
        csv.AppendLine("Match,Team,Possession %,Shots,Shots on Target,Passes,Pass Accuracy %");
        csv.AppendLine("Bayern vs Dortmund,Bayern München,65,18,8,425,89");
        csv.AppendLine("Bayern vs Dortmund,Borussia Dortmund,35,12,4,298,82");
        return Task.FromResult(csv.ToString());
    }
}

/// <summary>
/// Request model for CSV upload to Google Sheets
/// </summary>
public class CsvUploadRequest
{
    /// <summary>
    /// The ID of the Google Sheets spreadsheet
    /// </summary>
    public string SpreadsheetId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the sheet to upload to
    /// </summary>
    public string SheetName { get; set; } = "Sheet1";

    /// <summary>
    /// CSV data as string
    /// </summary>
    public string CsvData { get; set; } = string.Empty;

    /// <summary>
    /// Whether the CSV has headers in the first row
    /// </summary>
    public bool HasHeaders { get; set; } = true;

    /// <summary>
    /// Whether to clear existing data before uploading
    /// </summary>
    public bool ClearExistingData { get; set; } = true;

    /// <summary>
    /// Authentication options (optional, uses default from config)
    /// </summary>
    public GoogleSheetsAuthOptions? AuthOptions { get; set; }
}

/// <summary>
/// Request model for creating a new spreadsheet with CSV data
/// </summary>
public class SpreadsheetCreationRequest
{
    /// <summary>
    /// Title for the new spreadsheet
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Name for the initial sheet
    /// </summary>
    public string SheetName { get; set; } = "Sheet1";

    /// <summary>
    /// CSV data to populate the spreadsheet
    /// </summary>
    public string CsvData { get; set; } = string.Empty;

    /// <summary>
    /// Whether the CSV has headers in the first row
    /// </summary>
    public bool HasHeaders { get; set; } = true;

    /// <summary>
    /// Authentication options (optional, uses default from config)
    /// </summary>
    public GoogleSheetsAuthOptions? AuthOptions { get; set; }
}

/// <summary>
/// Request model for uploading Bundesliga data to Google Sheets
/// </summary>
public class BundesligaUploadRequest
{
    /// <summary>
    /// The ID of the Google Sheets spreadsheet
    /// </summary>
    public string SpreadsheetId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the sheet to upload to
    /// </summary>
    public string SheetName { get; set; } = "Bundesliga_Data";

    /// <summary>
    /// Type of Bundesliga data to export
    /// </summary>
    public BundesligaDataType DataType { get; set; } = BundesligaDataType.Matches;

    /// <summary>
    /// Whether to clear existing data before uploading
    /// </summary>
    public bool ClearExistingData { get; set; } = true;

    /// <summary>
    /// Authentication options (optional, uses default from config)
    /// </summary>
    public GoogleSheetsAuthOptions? AuthOptions { get; set; }
}

/// <summary>
/// Types of Bundesliga data that can be exported
/// </summary>
public enum BundesligaDataType
{
    /// <summary>
    /// Match results and basic information
    /// </summary>
    Matches,
    /// <summary>
    /// All match events (goals, cards, substitutions, etc.)
    /// </summary>
    Events,
    /// <summary>
    /// Goal-specific data with details
    /// </summary>
    Goals,
    /// <summary>
    /// Card-specific data
    /// </summary>
    Cards,
    /// <summary>
    /// Match statistics
    /// </summary>
    Statistics
}
