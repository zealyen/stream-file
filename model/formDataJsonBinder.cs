using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

public class FormDataJsonBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if(bindingContext == null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }    

        string fieldName = bindingContext.FieldName;
        var valueProviderResult = bindingContext.ValueProvider.GetValue(fieldName);

        if(valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }
        else
        {
            bindingContext.ModelState.SetModelValue(fieldName, valueProviderResult);
        }    

        string? value = valueProviderResult.FirstValue;
        if(string.IsNullOrEmpty(value))
        {
            return Task.CompletedTask;
        }

        try
        {                
            Partition[]? result = JsonSerializer.Deserialize<Partition[]>(value);
            bindingContext.Result = ModelBindingResult.Success(result);
        }
        catch(JsonException)
        {
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }
}