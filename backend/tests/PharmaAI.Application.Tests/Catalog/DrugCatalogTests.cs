using PharmaAI.Application.Catalog;
using PharmaAI.Application.Catalog.SearchCatalog;

namespace PharmaAI.Application.Tests.Catalog;

public sealed class DrugCatalogTests
{
    private static CatalogDrug Drug(string en, string ar, string scientific = "PARACETAMOL", decimal price = 10m) =>
        new(en, ar, scientific, "GSK", "ANALGESIC", "ORAL.SOLID", price);

    private static readonly DrugCatalog Catalog = new(
    [
        Drug("PANADOL EXTRA 24 F.C.TABS.", "بانادول إكسترا", "CAFFEINE+PARACETAMOL", 63m),
        Drug("PANADOL 500 MG 24 TABS.", "بانادول"),
        Drug("ABIMOL 500 MG 20 TABS.", "أبيمول"),
        Drug("CETAL 120MG/5ML SYRUP 100 ML", "سيتال"),
        Drug("AUGMENTIN 1 GM 14 F.C. TABS.", "أوجمنتين", "AMOXICILLIN+CLAVULANIC ACID"),
    ]);

    [Fact]
    public void Arabic_search_ignores_hamza_forms_and_case_of_english()
    {
        Assert.Equal("بانادول إكسترا", Assert.Single(Catalog.Search("بانادول اكسترا", 10)).NameAr);
        Assert.Equal("أوجمنتين", Assert.Single(Catalog.Search("اوجمنتين", 10)).NameAr);
        Assert.Equal(2, Catalog.Search("panadol", 10).Count);
    }

    [Fact]
    public void Arabic_spellings_with_or_without_long_vowels_match()
    {
        Assert.Equal("أوجمنتين", Assert.Single(Catalog.Search("اوجمينتين", 10)).NameAr);
        Assert.Equal("سيتال", Assert.Single(Catalog.Search("ستال", 10)).NameAr);
    }

    [Fact]
    public void Names_starting_with_the_term_come_before_ingredient_matches()
    {
        var names = Catalog.Search("paracetamol", 10).Select(d => d.NameEn).ToList();
        Assert.Equal(4, names.Count);

        var results = Catalog.Search("cetal", 10);
        Assert.Equal("CETAL 120MG/5ML SYRUP 100 ML", results[0].NameEn);
    }

    [Fact]
    public void Every_word_must_match_and_punctuation_is_ignored()
    {
        Assert.Equal("PANADOL EXTRA 24 F.C.TABS.", Assert.Single(Catalog.Search("panadol fctabs", 10)).NameEn);
        Assert.Empty(Catalog.Search("panadol augmentin", 10));
        Assert.Empty(Catalog.Search("  ", 10));
    }

    [Fact]
    public void Csv_reader_handles_quotes_and_skips_cancelled_or_unpriced_rows()
    {
        const string csv =
            "commercial_name_en,commercial_name_ar,scientific_name,manufacturer,drug_class,route,price_egp\r\n" +
            "\"BRUFEN 400 MG, 30 TABS.\",بروفين . ./,IBUPROFEN,\"KAHIRA \"\"PHARM\"\"\",NSAID,ORAL.SOLID,45.5\n" +
            "LOSEC 20MG 7 CAPS.(CANCELLED),لوسك,OMEPRAZOLE,ASTRA,PPI,ORAL.SOLID,32.4\n" +
            "NO PRICE,بدون,X,Y,Z,ORAL.SOLID,\n";

        var drug = Assert.Single(CatalogCsv.Parse(new StringReader(csv)));
        Assert.Equal("BRUFEN 400 MG, 30 TABS.", drug.NameEn);
        Assert.Equal("بروفين", drug.NameAr);
        Assert.Equal("KAHIRA \"PHARM\"", drug.Manufacturer);
        Assert.Equal(45.5m, drug.Price);
    }

    [Theory]
    [InlineData("NEXIUM 40 MG 28 F.C.TABS.", "علبة", "شريط", 4)]
    [InlineData("BRUFEN 400 MG 30 TABS.", "علبة", "شريط", 3)]
    [InlineData("PANADOL ADVANCE 500 MG 24 F.C.TABS.", "علبة", "شريط", 2)]
    [InlineData("FLUDCONRIZOLE 150 MG 1 CAPS.", "علبة", "شريط", 1)]
    [InlineData("CHIROCAINE 5 MG/ML 10 AMP.", "علبة", "أمبول", 10)]
    [InlineData("VOMIFRATON 8MG/4ML 5 I.M./I.V./INF. AMP.", "علبة", "أمبول", 5)]
    [InlineData("SMECTA 10 SACHETS", "علبة", "كيس", 10)]
    [InlineData("AMOXIL FORT 250MG/5ML SUSP. 100ML", "زجاجة", "زجاجة", 1)]
    [InlineData("ZIAPHASE GEL 50GM", "أنبوبة", "أنبوبة", 1)]
    public void Units_are_guessed_from_the_trade_name(string name, string pack, string sub, int count)
    {
        Assert.Equal(new SuggestedUnits(pack, sub, count), CatalogUnits.Suggest(name));
    }

    [Fact]
    public async Task Query_returns_prices_in_piasters_and_suggested_units()
    {
        var results = await new SearchCatalogQueryHandler(Catalog).Handle(new SearchCatalogQuery("panadol extra"), default);

        var drug = Assert.Single(results);
        Assert.Equal(6300, drug.PublicPricePiasters);
        Assert.Equal("شريط", drug.SubUnitName);
        Assert.Equal(2, drug.SubUnitsPerPack);
    }
}
