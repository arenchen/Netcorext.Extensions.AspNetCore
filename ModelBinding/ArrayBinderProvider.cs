using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Netcorext.Extensions.AspNetCore.ModelBinding;

public class ArrayBinderProvider : IModelBinderProvider
{
    private readonly IEnumerable<IModelBinderProvider> _modelBinderProviders;

    public ArrayBinderProvider(IEnumerable<IModelBinderProvider> modelBinderProviders)
    {
        _modelBinderProviders = modelBinderProviders;
    }

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (!context.Metadata.IsEnumerableType || context.Metadata.BindingSource == null || (!context.Metadata.BindingSource.CanAcceptDataFrom(BindingSource.Path) && !context.Metadata.BindingSource.CanAcceptDataFrom(BindingSource.Query)))
            return null;

        var binders = _modelBinderProviders.Select(t => t.GetBinder(context))
                                           .Where(t => t != null);

        return new ArrayBinder(binders!);
    }
}