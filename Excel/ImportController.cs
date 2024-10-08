using System.Data;
using ClosedXML.Excel;
using DemoApi.Models;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;

namespace DemoApi.Excel;

/// <inheritdoc />
[ApiController]
[Route("api/[controller]")]
public class ImportController(ILogger<ImportController> logger, SurveryGenerator generator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<List<SurveryResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadExcelImport()
    {
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null)
            return BadRequest();

        try
        {
            await using var stream = file.OpenReadStream();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataset = reader.AsDataSet();
            var table = dataset.Tables[0];

            var surveys = ProcessRows(table.Rows.OfType<DataRow>().ToList(), HttpContext.RequestAborted);
            return Ok(surveys);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while processing excel file");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("generate")]
    [ProducesResponseType<List<SurveryResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMoreSurverys([FromQuery] int count = 100)
    {
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null)
            return BadRequest();

        try
        {
            await using var stream = file.OpenReadStream();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataset = reader.AsDataSet();
            var table = dataset.Tables[0];

            var surveys = ProcessRows(table.Rows.OfType<DataRow>().ToList(), HttpContext.RequestAborted);
            return Ok(generator.GenerateSurverys(surveys, count));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while generating more surveys");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("generateExcel/{count}")]
    [ProducesResponseType<List<SurveryResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult GeneratedSurverysAsExcel(string count = "100")
    {
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            logger.LogWarning($"file is null, stop here");
            return BadRequest();
        }
        
        logger.LogInformation($"file name: {file.FileName}");

        try
        {
            using var stream = file.OpenReadStream();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataset = reader.AsDataSet();
            var table = dataset.Tables[0];

            var rows = table.Rows.OfType<DataRow>().ToList();

            var wb = GenerateExcelFile(rows, int.Parse(count));
            
            // save to file
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
            wb.SaveAs(path);
            
            logger.LogInformation($"file: {path}");
            
            // return file
            return File(System.IO.File.ReadAllBytes(path), "application/ms-excel", Path.GetFileName(path));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while generating more surveys");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private XLWorkbook GenerateExcelFile(IList<DataRow> rows, int count)
    {
        var keys = ExtractKeysFromDataRows(rows);
        var labels = ExtractLabelsFromDataRows(rows);
        var seed = ProcessRows(rows);
        var surveys = generator.GenerateSurverys(seed, count);


        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Surveys");

        // write keys
        for (var i = 0; i < keys.Count; i++)
            ws.Cell(1, i + 1).Value = keys[i];

        // write labels
        for (var i = 0; i < labels.Count; i++)
            ws.Cell(2, i + 1).Value = labels[i];

        // write surveys
        {
            var i = 0;
            foreach (var survey in surveys)
            {
                var properties = survey.GetType().GetProperties();

                for (var j = 0; j < keys.Count; j++)
                {
                    var key = keys[j];
                    var property = properties.FirstOrDefault(p => p.Name == key);
                    if (property == null)
                        continue;

                    var value = property.GetValue(survey);
                    
                    if (property.PropertyType == typeof(bool) || Nullable.GetUnderlyingType(property.PropertyType) == typeof(bool))
                        ws.Cell(3 + i, j + 1).Value = (bool)(value ?? false) ? "1" : "0";
                    else
                        ws.Cell(3 + i, j + 1).Value = value?.ToString();
                }

                i++;
            }
        }

        return wb;
    }

    private IList<SurveryResult> ProcessRows(IList<DataRow> rows, CancellationToken stoppingToken = default)
    {
        var keys = ExtractKeysFromDataRows(rows);
        var labels = ExtractLabelsFromDataRows(rows);
        var records = ExtractRecordsFromDataRows(rows);


        var elements = new List<SurveryResult>();
        for (var i = 0; i < records[0].Count; i++)
            elements.Add(new());


        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var values = records[i];

            for (var j = 0; j < values.Count; j++)
            {
                var value = values[j];
                var element = elements[j];
                var property = element.GetType().GetProperty(key);
                if (property == null)
                    continue;

                if (string.IsNullOrEmpty(value?.Trim()))
                {
                    property.SetValue(element, null);
                    continue;
                }

                // parse the value to the correct type
                if (property.PropertyType == typeof(string) ||
                    Nullable.GetUnderlyingType(property.PropertyType) == typeof(string))
                    property.SetValue(element, value);
                else if (property.PropertyType == typeof(int) ||
                         Nullable.GetUnderlyingType(property.PropertyType) == typeof(int))
                    property.SetValue(element, int.Parse(value));
                else if (property.PropertyType == typeof(bool) ||
                         Nullable.GetUnderlyingType(property.PropertyType) == typeof(bool))
                    property.SetValue(element, value == "1");
                else if (property.PropertyType == typeof(DateTime) ||
                         Nullable.GetUnderlyingType(property.PropertyType) == typeof(DateTime))
                    property.SetValue(element, DateTime.Parse(value));
                else if (property.PropertyType == typeof(double) ||
                         Nullable.GetUnderlyingType(property.PropertyType) == typeof(double))
                    property.SetValue(element, double.Parse(value));
                else
                    logger.LogError($"Unknown type {property.PropertyType} for property {key}");
            }
        }

        return elements;
    }

    private IDictionary<int, string> ExtractKeysFromDataRows(IList<DataRow> rows) =>
        ConvertToDicWithIndex(rows[0]);

    private IDictionary<int, string> ExtractLabelsFromDataRows(IList<DataRow> rows) =>
        ConvertToDicWithIndex(rows[1]);

    private IDictionary<int, string> ConvertToDicWithIndex(DataRow columns)
    {
        var strings = columns
            .ItemArray
            .OfType<string>()
            .ToList();

        var dic = new Dictionary<int, string>();
        for (var i = 0; i < strings.Count; i++)
        {
            dic.Add(i, strings[i]);
        }

        return dic;
    }

    private IDictionary<int, IList<string?>> ExtractRecordsFromDataRows(IList<DataRow> rows)
    {
        var result = new Dictionary<int, IList<string?>>();

        foreach (var row in rows.Skip(2))
        {
            for (var i = 0; i < row.ItemArray.Length; i++)
            {
                if (!result.ContainsKey(i))
                    result.Add(i, new List<string?> { row.ItemArray[i]?.ToString() });
                else
                    result[i].Add(row.ItemArray[i]?.ToString());
            }
        }

        return result;
    }
}

/// <summary>
/// record that holds one exitement point of data
/// </summary>
/// <param name="column"></param>
/// <param name="value"></param>
public class ExcitementPointDataRecord(int column, object? value)
{
    public int Column { get; set; } = column;

    public object? Value { get; set; } = value;
}