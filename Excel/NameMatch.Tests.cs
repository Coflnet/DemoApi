using FluentAssertions;
using NUnit.Framework;

public class NameMatchTests
{
    [Test]
    public void Test1()
    {
        var result = ExcelController.MapColumns(new(){
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
    public void FullBrandnameContained()
    {
        var result = ExcelController.MapColumns(new()
        {
            ("bild zeitung", "Tageszeitung"),
            ("bild zeitung", "Zeitung"),
            ("bild.de", "Für die Bild Zeitung")
        });

        result.NoChangeNecessary
            .Count(tuple => tuple == "bild zeitung")
            .Should()
            .Be(2);

        result.Mapped
            .Count(tuple => tuple.Output == "bild zeitung")
            .Should()
            .Be(1);
    }
}