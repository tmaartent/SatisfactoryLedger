using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Translator;

internal static class FormConverter
{
    public static ItemForm ToItemForm(string form)
    {
        return form switch
        {
            "RF_LIQUID" or "RF_GAS" => ItemForm.Fluid,
            "RF_SOLID" => ItemForm.Solid,
            _ => throw new ArgumentException($"Ünknown form {form}")
        };
    } 
}