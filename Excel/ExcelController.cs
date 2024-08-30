using System.Data;
using System.Text;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

[ApiController]
[Route("api/[controller]")]
public class ExcelController : ControllerBase
{
    static ExcelController()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    [HttpPost]
    public MapingResult UploadExcelFile()
    {
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            return null;
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
            brand = brand.ToLower().Replace("und", "&").Replace(" &", "&").Replace("& ", "&");

            raw.Add((brand, product));
        }
        System.IO.File.WriteAllText("output.json", JsonConvert.SerializeObject(raw));
        return Map(raw);
    }

    [HttpGet]
    public MapingResult Get()
    {
        var raw = JsonConvert.DeserializeObject<List<(string brand, string product)>>(System.IO.File.ReadAllText("output.json"));
        return Map(raw);
    }

    private static MapingResult Map(List<(string brand, string product)> raw)
    {
        var brandIds = raw.GroupBy(x => x.brand.ToLower())
                    .Where(x => x.Count() > 2)
                    .ToDictionary(s => s.Key, s => s.Count());

        var fullGroup = raw.GroupBy(x => x.brand.ToLower())
            .Where(f => f.Count() > 1)
            .ToDictionary(s => s.GroupBy(s => s.brand).OrderByDescending(b => b.Count()).First().Key, s => s);

        var result = new MapingResult()
        {
            BrandOccurences = fullGroup.ToDictionary(x => x.Key, x => x.Value.GroupBy(p=>p.product).Select(y => new MapElement()
            {
                Brand = x.Key,
                Product = y.Key,
                OccuredTimes = y.Count()
            }).ToList())
        };

        foreach (var item in raw)
        {
            var lookup = item.brand.ToLower();
            if (brandIds.ContainsKey(lookup))
                continue;

            var best = brandIds.Keys.Select(b => (brand: b, distance: Fastenshtein.Levenshtein.Distance(b, lookup)))
                .OrderBy(x => x.distance)
                .FirstOrDefault();

            if (best.distance < 3)
            {
                var list = result.BrandOccurences.GetValueOrDefault(best.brand, new());
                list.Add(new MapElement()
                {
                    Brand = item.brand,
                    Product = item.product,
                    OccuredTimes = 1
                });
                result.BrandOccurences[best.brand] = list;
            }
            else
            {
                result.Unmappable.Add(new MapElement()
                {
                    Brand = item.brand,
                    Product = item.product,
                    OccuredTimes = 1
                });
            }
        }

        return result;
    }

    public class MapElement
    {
        public string Brand { get; set; }
        public string Product { get; set; }
        public int OccuredTimes { get; set; }
    }

    public class MapingResult
    {
        public Dictionary<string, List<MapElement>> BrandOccurences { get; set; } = new();
        public List<MapElement> Unmappable { get; set; } = new();
    }
}