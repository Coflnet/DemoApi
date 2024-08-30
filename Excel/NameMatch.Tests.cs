using FluentAssertions;
using NUnit.Framework;

public class NameMatchTests
{
    [Test]
    public void Test1()
    {
        var result = ExcelController.Map(new(){
            ("wir kaufen dein auto", "wir kaufen dein auto"),
            ("wir kaufen dein auto", "Auto Verkauf"),
            ("wir kaufen dein auto", "Auto ankauf"),
            ("wirkaufendeinauto.de","Autoankauf")});
        result.BrandOccurences["wir kaufen dein auto"].Count.Should().Be(4);
    }
    [Test]
    public void FullBrandnameContained()
    {
        var result = ExcelController.Map(new(){
            ("bild zeitung", "Tageszeitung"),
            ("bild zeitung", "Zeitung"),
            ("bild.de","Für die Bild Zeitung")});
        result.BrandOccurences["bild zeitung"].Count.Should().Be(3);
    }
}