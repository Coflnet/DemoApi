using Coflnet.Excel;
using FluentAssertions;
using Moq;
using NUnit.Framework;

public class NameMatchTests
{
    BrandMappingService ExcelController ;

    [SetUp]
    public void Setup()
    {
        var isCompany = new Mock<IIsCompanyService>();
        isCompany.Setup(x => x.CheckBatch(It.IsAny<List<string>>()))
            .ReturnsAsync(new Dictionary<string, bool>
            {
                {"wir kaufen dein auto", true},
                {"bild zeitung", true}
            });
        ExcelController = new BrandMappingService(isCompany.Object);
    }
    [Test]
    public async Task Test1()
    {
        var result = await ExcelController.MapColumns(new(){
            ("wir kaufen dein auto", "wir kaufen dein auto"),
            ("wir kaufen dein auto", "Auto Verkauf"),
            ("wir kaufen dein auto", "Auto ankauf"),
            ("wirkaufendeinauto.de","Autoankauf")});
        
        result.Mapped
            .Count(tuple => tuple.Output == "wir kaufen dein auto")
            .Should()
            .Be(1);

        result.NoChangeNecessary
            .Count(tuple => tuple == "wir kaufen dein auto")
            .Should()
            .Be(3);
    }

    [Test]
    public async Task FullBrandnameContained()
    {
        var result = await ExcelController.MapColumns(new()
        {
            ("bild zeitung", "Tageszeitung"),
            ("bild zeitung", "Zeitung"),
            ("bild zeitung", "Zeitung"),
            ("bild.de", "Für die Bild Zeitung")
        });

        result.NoChangeNecessary
            .Count(tuple => tuple == "bild zeitung")
            .Should()
            .Be(3);

        result.Mapped
            .Count(tuple => tuple.Output == "bild zeitung")
            .Should()
            .Be(1);
    }
}