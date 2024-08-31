using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Cassandra.Mapping;
using Coflnet.Excel;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

[ApiController]
[Route("api/[controller]")]
public class ExcelController : ControllerBase
{
    private BrandMappingService brandMappingService;
    private ILogger<ExcelController> logger;

    public ExcelController(BrandMappingService brandMappingService, ILogger<ExcelController> logger)
    {
        this.brandMappingService = brandMappingService;
        this.logger = logger;
    }

    static ExcelController()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [HttpGet("brand")]
    public async Task<IActionResult> Get([FromServices] IIsCompanyService isCompanyService, string company)
    {
        return Ok(await isCompanyService.CheckBatch(new List<string> { company }));
    }

    [HttpPost]
    [ProducesResponseType<MappingResult2>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadExcelFile()
    {
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            return BadRequest();
        }

        using var stream = file.OpenReadStream();
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataset = reader.AsDataSet();
        var table = dataset.Tables[0];
        var raw = new List<(string brand, string product)>();

        // iterate over rows
        foreach (DataRow row in table.Rows)
        {
            if (row.ItemArray.Length == 0 || row.ItemArray[0]?.ToString() != "Ja")
            {
                continue;
            }

            var brand = row.ItemArray[1]?.ToString();
            var product = row.ItemArray[2]?.ToString();
            if (brand.Length < 2)
                continue;

            raw.Add((brand, product));
        }

        System.IO.File.WriteAllText("output.json", JsonConvert.SerializeObject(raw));
        return Ok(await brandMappingService.MapColumns(raw));
    }

    [HttpGet]
    public async Task<MapingResult> Get()
    {
        var raw = JsonConvert.DeserializeObject<List<(string brand, string product)>>(
            System.IO.File.ReadAllText("output.json"));
        return await brandMappingService.Map(raw);
    }
}


public class MapElement
{
    public string Brand { get; set; }
    public string Product { get; set; }
    public int OccuredTimes { get; set; }
    public string By { get; set; }
}

public class MapingResult
{
    public Dictionary<string, List<MapElement>> BrandOccurences { get; set; } = new();
    public List<MapElement> Unmappable { get; set; } = new();
}

public class MappingResult2
{
    public List<string> Brands { get; set; }
    public List<string> NoChangeNecessary { get; set; }
    public List<MappingElement> Mapped { get; set; }
    public List<UnMappable> Unmappable { get; set; }
}

public class MappingElement(string input, string product, string output)
{
    public string InputBrand { get; set; } = input;
    public string InputProduct { get; set; } = product;

    public string Output { get; set; } = output;
}

public class UnMappable(string input, string product)
{
    public string InputBrand { get; set; } = input;
    public string InputProduct { get; set; } = product;
}