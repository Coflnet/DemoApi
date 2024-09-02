using System.Data;
using Coflnet.Excel;
using Newtonsoft.Json;

public class BrandMappingService
{
    private IIsCompanyService isCompanyService;

    public BrandMappingService(IIsCompanyService isCompanyService)
    {
        this.isCompanyService = isCompanyService;
    }
    BrandMappingService()
    {
        foreach (var brand in BrandsFull)
        {
            Brands[GetLookupKey(brand)] = brand;
        }
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

    public async Task<MapingResult> Map(List<(string brand, string product)> raw)
    {
        var fullGroup = await GroupByValidBrands(raw);
        var brandIds = fullGroup
                .ToDictionary(x => GetLookupKey(x.Key), x => x.Key);
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
                continue;
            }

            if (Brands.TryGetValue(lookup, out var name))
            {
                AddMatch(result, item, name);
                continue;
            }

            var best = brandIds.Keys.Select(b => (brand: b, distance: Fastenshtein.Levenshtein.Distance(b, lookup)))
                .OrderBy(x => x.distance)
                .FirstOrDefault();
            if (best.distance < Math.Min(4, lookup.Length / 2))
            {
                var fullName = brandIds[best.brand];
                AddMatch(result, item, fullName, "brand");

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
                continue;
            }

            var containing = GetContaining(brandIds, lookup, product, item);
            if (containing.Value != default)
            {
                AddMatch(result, item, containing.Value, "containing");
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

        return result;
    }

    private async Task<Dictionary<string, IGrouping<string, (string brand, string product)>>> GroupByValidBrands(List<(string brand, string product)> raw)
    {
        var fullGroup = raw.GroupBy(x => GetLookupKey(x.brand))
            .Where(f => f.Count() > 2 && IsBrand(f.Key))
            .ToDictionary(s => s.GroupBy(s => s.brand).OrderByDescending(b => b.Count()).First().Key, s => s);

        var isbrandLookup = await isCompanyService.CheckBatch(fullGroup.Keys.ToList());
        fullGroup = fullGroup.Where(x => isbrandLookup.GetValueOrDefault(x.Key, true)).ToDictionary(x => x.Key, x => x.Value);
        Console.WriteLine(JsonConvert.SerializeObject(isbrandLookup, Formatting.Indented));
        return fullGroup;
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

    static bool IsBrand(string brand)
    {
        Console.WriteLine($"checking if {brand} is a brand");
        var forbiddenKeywords = new[] { "keine", "kein", "weiß nicht", "weiss nicht" };

        if (forbiddenKeywords.Contains(brand.ToLower()))
            return false;

        return true;
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

    internal async Task<MappingResult2> MapColumns(List<(string brand, string product)> raw)
    {
        var baseMap = await Map(raw);
        return new()
        {
            Brands = baseMap.BrandOccurences.Keys.ToList(),
            NoChangeNecessary = baseMap.BrandOccurences.SelectMany(x => Enumerable.Repeat(x.Key, x.Value.Where(v => v.Brand == x.Key).Sum(v => v.OccuredTimes))).ToList(),
            Mapped = baseMap.BrandOccurences.SelectMany(x => x.Value.Where(v => v.Brand != x.Key).SelectMany(b => Enumerable.Repeat(new MappingElement(b.Brand, b.Product, x.Key), b.OccuredTimes))).ToList(),
            Unmappable = baseMap.Unmappable.Select(x => new UnMappable(x.Brand, x.Product)).ToList()
        };
    }

}
