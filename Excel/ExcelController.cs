using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Cassandra.Mapping;
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

        foreach (var item in BrandsFull.ToList())
        {
            Brands.Add(GetLookupKey(item), item);
        }
    }

    [HttpPost]
    [ProducesResponseType<MappingResult2>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult UploadExcelFile()
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
        return Ok(Map(raw));
    }

    [HttpGet]
    public MappingResult2 Get()
    {
        var raw = JsonConvert.DeserializeObject<List<(string brand, string product)>>(
            System.IO.File.ReadAllText("output.json"));
        return Map(raw);
    }


    private static HashSet<string> BrandsFull =
    [
        "Netflix", "Apple", "Nike", "Target", "Google", "Amazon", "Spotify", "Disney", "ROBLOX", "Vans", "Nintendo",
        "Headspace", "REI", "Lego", "Delta", "Microsoft", "Rockstar", "CHANEL", "LinkedIn", "Uniqlo", "PlayStation",
        "Tesla", "Starbucks", "NVIDIA",
        "Salesforce", "Honda", "Audi", "Red Bull", "Mercedes-Benz", "Hershey", "Dunkin'", "Porsche", "Chipotle",
        "BMW Group", "Pinterest", "Logitech", "Shopify", "Crocs", "Gucci", "AMD", "Coca-Cola", "adidas", "Mars",
        "American Express", "PUMA", "Versace",
        "Visa", "Adobe", "Cisco", "Airbnb", "Toyota", "Tommy Hilfiger", "Hilton", "McDonald's", "Mastercard", "Uber",
        "Coinbase", "FedEx", "3M", "Nordstrom", "Philips", "Bose", "Foot Locker", "Bosch",
        "langnese", "haribo", "nothing", "dell", "expert", "ratiopharm", "aldi süd", "o.b.", "apollo", "trumpf",
        "duplo", "milka", "nivea", "xbox", "gmx", "jaguar", "Ubisoft", "norma"
    ];

    private static Dictionary<string, string> Brands = new();

    public static MappingResult2 Map(List<(string brand, string product)> raw)
    {
        var fullGroup = raw.GroupBy(x => GetLookupKey(x.brand))
            .Where(f => f.Count() > 1)
            .ToDictionary(s => s.GroupBy(s => s.brand).OrderByDescending(b => b.Count()).First().Key, s => s);

        var brandIds = fullGroup.ToDictionary(x => GetLookupKey(x.Key), x => x.Key);
        foreach (var item in Brands)
        {
            brandIds.TryAdd(item.Key, item.Value);
        }

        foreach (var item in brandIds.Keys)
        {
            foreach (var secondKey in brandIds.Keys)
            {
                var distence = Fastenshtein.Levenshtein.Distance(item, secondKey);
                if (distence != 1 || item.Length <= 4)
                {
                    continue;
                }

                // remove the shorter one
                if ((item.Length > secondKey.Length || Brands.ContainsKey(item)) && brandIds.ContainsKey(secondKey))
                {
                    fullGroup.Remove(brandIds[secondKey]);
                    brandIds.Remove(secondKey);
                }
                else if (brandIds.ContainsKey(item))
                {
                    fullGroup.Remove(brandIds[item]);
                    brandIds.Remove(item);
                }
            }
        }

        var result = new MapingResult()
        {
            BrandOccurences = fullGroup.ToDictionary(x => x.Key, x => x.Value.GroupBy(p => p.product).Select(y =>
                new MapElement()
                {
                    Brand = x.Key,
                    Product = y.Key,
                    OccuredTimes = y.Count()
                }).ToList())
        };

        var result2 = new MappingResult2()
        {
            Brands = brandIds.Values.ToList(),
            NoChangeNecessary = [],
            Mapped = [],
            Unmappable = []
        };

        foreach (var item in raw)
        {
            var lookup = GetLookupKey(item.brand);
            var product = GetLookupKey(item.product);
            if (brandIds.ContainsKey(lookup))
            {
                result2.NoChangeNecessary.Add(new(item.brand, item.brand));
                continue;
            }

            if (Brands.TryGetValue(lookup, out var name))
            {
                AddMatch(result, item, name);
                result2.NoChangeNecessary.Add(new(item.brand, item.brand));
                continue;
            }

            var best = brandIds.Keys.Select(b => (brand: b, distance: Fastenshtein.Levenshtein.Distance(b, lookup)))
                .OrderBy(x => x.distance)
                .FirstOrDefault();
            if (best.distance < Math.Min(4, lookup.Length / 2))
            {
                var fullName = brandIds[best.brand];
                AddMatch(result, item, fullName, "brand");

                if (best.distance == 0)
                    result2.NoChangeNecessary.Add(new(item.brand, item.brand));
                else
                    result2.Mapped.Add(new(item.brand, best.brand));

                continue;
            }

            // retry with product
            var bestProduct = brandIds.Keys
                .Select(b => (brand: b, distance: Fastenshtein.Levenshtein.Distance(b, product)))
                .OrderBy(x => x.distance)
                .FirstOrDefault();

            if (bestProduct.distance < Math.Min(4, lookup.Length / 4))
            {
                AddMatch(result, item, brandIds[bestProduct.brand], "product");
                if (bestProduct.distance == 0)
                    result2.NoChangeNecessary.Add(new(item.brand, item.brand));
                else
                    result2.Mapped.Add(new(item.brand, bestProduct.brand));

                continue;
            }

            var containing = GetContaining(brandIds, lookup, product, item);
            if (containing.Value != default)
            {
                AddMatch(result, item, containing.Value, "containing");
                result2.Mapped.Add(new(item.brand, containing.Value));

                continue;
            }

            result.Unmappable.Add(new MapElement()
            {
                Brand = item.brand,
                Product = item.product,
                OccuredTimes = 1
            });
            result2.Unmappable.Add(new(item.brand, item.brand));
        }

        return result2;
    }

    static void AddMatch(MapingResult result, (string brand, string product) item, string name, string? by = null)
    {
        var list = result.BrandOccurences.GetValueOrDefault(name, new());
        list.Add(new MapElement()
        {
            Brand = item.brand,
            Product = item.product,
            OccuredTimes = 1
        });
        result.BrandOccurences[name] = list;
    }

    static string GetLookupKey(string val)
    {
        return val.ToLower().Replace("und", "&").Replace(" &", "&").Replace("& ", "&").Replace("für ", "")
            .Replace(" ", "").Replace(".", "").Replace("'", "").Replace("’", "");
    }

    private static KeyValuePair<string, string> GetContaining(Dictionary<string, string> brandIds, string lookup,
        string product, (string brand, string product) item)
    {
        return brandIds.FirstOrDefault(b => (product.Contains(b.Key) || lookup.Contains(b.Key)) && (b.Key.Length > 3));
    }

    public class MapElement
    {
        public string Brand { get; set; }
        public string Product { get; set; }
        public int OccuredTimes { get; set; }
        public string By {get;set;}
    }

    public class MapingResult
    {
        public Dictionary<string, List<MapElement>> BrandOccurences { get; set; } = new();
        public List<MapElement> Unmappable { get; set; } = new();
    }

    public class MappingResult2
    {
        public List<string> Brands { get; set; }

        public List<MappingElement> NoChangeNecessary { get; set; }

        public List<MappingElement> Mapped { get; set; }

        public List<MappingElement> Unmappable { get; set; }
    }

    public class MappingElement(string input, string output)
    {
        public string Input { get; set; } = input;

        public string Output { get; set; } = output;
    }
}