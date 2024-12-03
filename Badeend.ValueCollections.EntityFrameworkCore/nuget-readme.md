
Extension methods to asynchronously fetch `IQueryable<T>` results directly into collection types provided by `Badeend.ValueCollections`.

This includes:
- `.ToValueListAsync()`
- `.ToValueSetAsync()`
- `.ToValueDictionaryAsync()`

This package only supports `IQueryable<T>` instances created by `Microsoft.EntityFrameworkCore`.

More information at: https://badeend.github.io/ValueCollections/