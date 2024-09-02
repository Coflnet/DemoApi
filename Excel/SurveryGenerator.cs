using DemoApi.Models;

namespace DemoApi.Excel;

public class SurveryGenerator
{
    public IEnumerable<SurveryResult> GenerateSurverys(IList<SurveryResult> seed, int count)
    {
        foreach (var _ in Enumerable.Range(0, count))
        {
             var sample = GenerateNewSurvery(seed);
             seed.Add(sample);
             yield return sample;
        }
    }
    
    private SurveryResult GenerateNewSurvery(IList<SurveryResult> seed)
    {
        var result = new SurveryResult();
        var properties = typeof(SurveryResult).GetProperties();

        foreach (var property in properties)
        {
            var value = property.Name switch
            {
                nameof(SurveryResult.RecordNo) => seed.Select(s => s.RecordNo).Max() + 1,
                _ => property.GetValue(seed[new Random().Next(seed.Count)])
            };
            
            property.SetValue(result, value);
        }

        return result;
    }
}