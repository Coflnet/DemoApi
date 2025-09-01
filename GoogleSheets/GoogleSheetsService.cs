using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.Text;

namespace Coflnet.GoogleSheets;

/// <summary>
/// Service for managing Google Sheets operations including CSV upload
/// </summary>
public class GoogleSheetsService : IDisposable
{
    private readonly SheetsService sheetsService;
    private readonly ILogger<GoogleSheetsService> logger;
    private readonly string applicationName;

    /// <summary>
    /// Initializes a new instance of the GoogleSheetsService
    /// </summary>
    /// <param name="credential">Google OAuth2 credential</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="applicationName">Application name for Google API requests</param>
    public GoogleSheetsService(GoogleCredential credential, ILogger<GoogleSheetsService> logger, string applicationName = "DemoApi")
    {
        this.logger = logger;
        this.applicationName = applicationName;
        
        sheetsService = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = applicationName,
        });
    }

    /// <summary>
    /// Uploads CSV data to a Google Sheets document
    /// </summary>
    /// <param name="spreadsheetId">The ID of the Google Sheets document</param>
    /// <param name="sheetName">The name of the sheet to write to</param>
    /// <param name="csvData">CSV data as string</param>
    /// <param name="hasHeaders">Whether the CSV has headers in the first row</param>
    /// <param name="clearExistingData">Whether to clear existing data before writing</param>
    /// <returns>Update result with details about the operation</returns>
    public async Task<SheetUpdateResult> UploadCsvToSheetAsync(
        string spreadsheetId, 
        string sheetName, 
        string csvData, 
        bool hasHeaders = true,
        bool clearExistingData = true)
    {
        try
        {
            logger.LogInformation($"Starting CSV upload to sheet '{sheetName}' in spreadsheet '{spreadsheetId}'");

            // Parse CSV data
            var rows = ParseCsvData(csvData);
            if (!rows.Any())
            {
                throw new ArgumentException("CSV data is empty or invalid");
            }

            // Ensure sheet exists
            await EnsureSheetExistsAsync(spreadsheetId, sheetName);

            // Clear existing data if requested
            if (clearExistingData)
            {
                await ClearSheetDataAsync(spreadsheetId, sheetName);
            }

            // Prepare the range
            var range = $"{sheetName}!A1";
            
            // Convert rows to ValueRange
            var valueRange = new ValueRange
            {
                Values = rows.Cast<IList<object>>().ToList()
            };

            // Update the sheet
            var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            
            var response = await updateRequest.ExecuteAsync();

            // Format headers if they exist
            if (hasHeaders && rows.Count > 0)
            {
                await FormatHeaderRowAsync(spreadsheetId, sheetName, rows[0].Count);
            }

            // Auto-resize columns
            await AutoResizeColumnsAsync(spreadsheetId, sheetName);

            var result = new SheetUpdateResult
            {
                Success = true,
                SpreadsheetId = spreadsheetId,
                SheetName = sheetName,
                RowsUpdated = response.UpdatedRows ?? 0,
                ColumnsUpdated = response.UpdatedColumns ?? 0,
                CellsUpdated = response.UpdatedCells ?? 0,
                Message = $"Successfully uploaded {rows.Count} rows to sheet '{sheetName}'"
            };

            logger.LogInformation($"CSV upload completed. Updated {result.RowsUpdated} rows, {result.ColumnsUpdated} columns, {result.CellsUpdated} cells");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error uploading CSV to sheet '{sheetName}' in spreadsheet '{spreadsheetId}'");
            return new SheetUpdateResult
            {
                Success = false,
                SpreadsheetId = spreadsheetId,
                SheetName = sheetName,
                Message = $"Error uploading CSV: {ex.Message}",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates a new spreadsheet with initial data from CSV
    /// </summary>
    /// <param name="title">Title for the new spreadsheet</param>
    /// <param name="csvData">CSV data to populate the spreadsheet</param>
    /// <param name="sheetName">Name for the initial sheet</param>
    /// <param name="hasHeaders">Whether the CSV has headers</param>
    /// <returns>Creation result with spreadsheet details</returns>
    public async Task<SpreadsheetCreationResult> CreateSpreadsheetFromCsvAsync(
        string title, 
        string csvData, 
        string sheetName = "Sheet1",
        bool hasHeaders = true)
    {
        try
        {
            logger.LogInformation($"Creating new spreadsheet '{title}' with CSV data");

            // Parse CSV data
            var rows = ParseCsvData(csvData);
            if (!rows.Any())
            {
                throw new ArgumentException("CSV data is empty or invalid");
            }

            // Create new spreadsheet
            var spreadsheet = new Spreadsheet
            {
                Properties = new SpreadsheetProperties
                {
                    Title = title
                },
                Sheets = new List<Sheet>
                {
                    new Sheet
                    {
                        Properties = new SheetProperties
                        {
                            Title = sheetName,
                            GridProperties = new GridProperties
                            {
                                RowCount = rows.Count,
                                ColumnCount = rows[0].Count
                            }
                        }
                    }
                }
            };

            var createRequest = sheetsService.Spreadsheets.Create(spreadsheet);
            var createdSpreadsheet = await createRequest.ExecuteAsync();

            // Add CSV data to the new spreadsheet
            var uploadResult = await UploadCsvToSheetAsync(
                createdSpreadsheet.SpreadsheetId, 
                sheetName, 
                csvData, 
                hasHeaders, 
                false); // Don't clear since it's a new sheet

            var result = new SpreadsheetCreationResult
            {
                Success = uploadResult.Success,
                SpreadsheetId = createdSpreadsheet.SpreadsheetId,
                SpreadsheetUrl = createdSpreadsheet.SpreadsheetUrl,
                Title = title,
                SheetName = sheetName,
                RowsAdded = uploadResult.RowsUpdated,
                Message = uploadResult.Success 
                    ? $"Successfully created spreadsheet '{title}' with {uploadResult.RowsUpdated} rows"
                    : $"Created spreadsheet but failed to add data: {uploadResult.Error}",
                Error = uploadResult.Error
            };

            logger.LogInformation($"Spreadsheet creation completed. ID: {result.SpreadsheetId}, URL: {result.SpreadsheetUrl}");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error creating spreadsheet '{title}'");
            return new SpreadsheetCreationResult
            {
                Success = false,
                Title = title,
                Message = $"Error creating spreadsheet: {ex.Message}",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets information about a spreadsheet
    /// </summary>
    /// <param name="spreadsheetId">The ID of the spreadsheet</param>
    /// <returns>Spreadsheet information</returns>
    public async Task<SpreadsheetInfo> GetSpreadsheetInfoAsync(string spreadsheetId)
    {
        try
        {
            var request = sheetsService.Spreadsheets.Get(spreadsheetId);
            var spreadsheet = await request.ExecuteAsync();

            return new SpreadsheetInfo
            {
                SpreadsheetId = spreadsheet.SpreadsheetId,
                Title = spreadsheet.Properties.Title,
                Url = spreadsheet.SpreadsheetUrl,
                Sheets = spreadsheet.Sheets?.Select(s => new SheetInfo
                {
                    SheetId = s.Properties.SheetId ?? 0,
                    Title = s.Properties.Title,
                    RowCount = s.Properties.GridProperties?.RowCount ?? 0,
                    ColumnCount = s.Properties.GridProperties?.ColumnCount ?? 0
                }).ToList() ?? new List<SheetInfo>()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting spreadsheet info for ID: {spreadsheetId}");
            throw;
        }
    }

    private List<List<string>> ParseCsvData(string csvData)
    {
        var rows = new List<List<string>>();
        var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var columns = ParseCsvLine(line);
            if (columns.Any())
            {
                rows.Add(columns);
            }
        }

        return rows;
    }

    private List<string> ParseCsvLine(string line)
    {
        var columns = new List<string>();
        var inQuotes = false;
        var currentColumn = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentColumn.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                columns.Add(currentColumn.ToString());
                currentColumn.Clear();
            }
            else
            {
                currentColumn.Append(ch);
            }
        }

        columns.Add(currentColumn.ToString());
        return columns;
    }

    private async Task EnsureSheetExistsAsync(string spreadsheetId, string sheetName)
    {
        try
        {
            var spreadsheet = await sheetsService.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
            var sheetExists = spreadsheet.Sheets?.Any(s => s.Properties.Title == sheetName) ?? false;

            if (!sheetExists)
            {
                logger.LogInformation($"Creating new sheet '{sheetName}' in spreadsheet '{spreadsheetId}'");

                var addSheetRequest = new AddSheetRequest
                {
                    Properties = new SheetProperties
                    {
                        Title = sheetName
                    }
                };

                var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = new List<Request>
                    {
                        new Request { AddSheet = addSheetRequest }
                    }
                };

                await sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId).ExecuteAsync();
                logger.LogInformation($"Successfully created sheet '{sheetName}'");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error ensuring sheet '{sheetName}' exists");
            throw;
        }
    }

    private async Task ClearSheetDataAsync(string spreadsheetId, string sheetName)
    {
        try
        {
            var range = $"{sheetName}!A:ZZ"; // Clear all data
            var clearRequest = sheetsService.Spreadsheets.Values.Clear(new ClearValuesRequest(), spreadsheetId, range);
            await clearRequest.ExecuteAsync();
            logger.LogDebug($"Cleared existing data in sheet '{sheetName}'");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error clearing data in sheet '{sheetName}'");
            throw;
        }
    }

    private async Task FormatHeaderRowAsync(string spreadsheetId, string sheetName, int columnCount)
    {
        try
        {
            var spreadsheet = await sheetsService.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets?.FirstOrDefault(s => s.Properties.Title == sheetName);
            
            if (sheet?.Properties?.SheetId == null) return;

            var requests = new List<Request>
            {
                new Request
                {
                    RepeatCell = new RepeatCellRequest
                    {
                        Range = new GridRange
                        {
                            SheetId = sheet.Properties.SheetId,
                            StartRowIndex = 0,
                            EndRowIndex = 1,
                            StartColumnIndex = 0,
                            EndColumnIndex = columnCount
                        },
                        Cell = new CellData
                        {
                            UserEnteredFormat = new CellFormat
                            {
                                TextFormat = new TextFormat
                                {
                                    Bold = true
                                },
                                BackgroundColor = new Color
                                {
                                    Red = 0.9f,
                                    Green = 0.9f,
                                    Blue = 0.9f,
                                    Alpha = 1.0f
                                }
                            }
                        },
                        Fields = "userEnteredFormat(textFormat,backgroundColor)"
                    }
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest { Requests = requests };
            await sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId).ExecuteAsync();
            
            logger.LogDebug($"Applied header formatting to sheet '{sheetName}'");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, $"Failed to format header row in sheet '{sheetName}'. Data was uploaded successfully.");
        }
    }

    private async Task AutoResizeColumnsAsync(string spreadsheetId, string sheetName)
    {
        try
        {
            var spreadsheet = await sheetsService.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets?.FirstOrDefault(s => s.Properties.Title == sheetName);
            
            if (sheet?.Properties?.SheetId == null) return;

            var requests = new List<Request>
            {
                new Request
                {
                    AutoResizeDimensions = new AutoResizeDimensionsRequest
                    {
                        Dimensions = new DimensionRange
                        {
                            SheetId = sheet.Properties.SheetId,
                            Dimension = "COLUMNS"
                        }
                    }
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest { Requests = requests };
            await sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId).ExecuteAsync();
            
            logger.LogDebug($"Auto-resized columns in sheet '{sheetName}'");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, $"Failed to auto-resize columns in sheet '{sheetName}'. Data was uploaded successfully.");
        }
    }

    /// <summary>
    /// Disposes the Google Sheets service
    /// </summary>
    public void Dispose()
    {
        sheetsService?.Dispose();
    }
}

/// <summary>
/// Result of a sheet update operation
/// </summary>
public class SheetUpdateResult
{
    public bool Success { get; set; }
    public string SpreadsheetId { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public int RowsUpdated { get; set; }
    public int ColumnsUpdated { get; set; }
    public int CellsUpdated { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
}

/// <summary>
/// Result of a spreadsheet creation operation
/// </summary>
public class SpreadsheetCreationResult
{
    public bool Success { get; set; }
    public string SpreadsheetId { get; set; } = string.Empty;
    public string SpreadsheetUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public int RowsAdded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
}

/// <summary>
/// Information about a spreadsheet
/// </summary>
public class SpreadsheetInfo
{
    public string SpreadsheetId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<SheetInfo> Sheets { get; set; } = new();
}

/// <summary>
/// Information about a sheet within a spreadsheet
/// </summary>
public class SheetInfo
{
    public int SheetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
}
