using Microsoft.ApplicationInsights.Extensibility.Implementation;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.GameData.Translator;

namespace SatisfactoryLedger.Catalog.Tests.GameData.Translator;

public class FormConverterTests
{
    [Theory]
    [InlineData("RF_LIQUID", ItemForm.Fluid)]
    [InlineData("RF_GAS", ItemForm.Fluid)]
    [InlineData("RF_SOLID", ItemForm.Solid)]
    public void ToItemForm_can_covert_correctly(string form, ItemForm expected)
    {
        Assert.Equal(expected, FormConverter.ToItemForm(form));
    }

    [Fact]
    public void ToItemForm_throws_exception_if_form_not_found()
    {
        Assert.Throws<ArgumentException>(() => FormConverter.ToItemForm("RF_SOLIDD"));
    }
}