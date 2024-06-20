<p align="center">
  <img src="./images/logo.png" alt="ValueCollections" width="400"/>
</p>

# Introduction

This package provides _immutable_ collections with _value equality_:
- **Immutability**: Once constructed, the collections cannot be changed anymore. Efficient construction can be done using so called Builders.
- **Value equality** (a.k.a. structural equality): Two collections are considered "equal" when they have the same type and the same content.

The combination of these two properties neatly complement C# [`record`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record) types and streamline the implementation of [Value Objects (DDD)](https://en.wikipedia.org/wiki/Value_object).

In general, the performance and memory usage is equivalent to the regular `System.Collections.Generic` types. Converting a Builder to an immutable instance is an `O(1)` operation. More information on this can be found at [Why & how?](./rationale.md). Along with:
- Comparison with `System.Collections.Immutable`
- Comparison with `IReadOnlyCollection/List/Set/Dictionary`

> Preface: The examples in this document focus on ValueLists, but the same principles apply equally to [ValueSets](https://badeend.github.io/ValueCollections/api/Badeend.ValueCollections.ValueSet-1.html) and [ValueDictionaries](https://badeend.github.io/ValueCollections/api/Badeend.ValueCollections.ValueDictionary-2.html).

## Installation

```sh
dotnet add package Badeend.ValueCollections

# Optional:
dotnet add package Badeend.ValueCollections.SystemTextJson
dotnet add package Badeend.ValueCollections.NewtonsoftJson
```

Nuget packages:
- [![NuGet Badeend.ValueCollections](https://img.shields.io/nuget/v/Badeend.ValueCollections?label=Badeend.ValueCollections)](https://www.nuget.org/packages/Badeend.ValueCollections)
- [![NuGet Badeend.ValueCollections.SystemTextJson](https://img.shields.io/nuget/v/Badeend.ValueCollections.SystemTextJson?label=Badeend.ValueCollections.SystemTextJson)](https://www.nuget.org/packages/Badeend.ValueCollections.SystemTextJson)
- [![NuGet Badeend.ValueCollections.NewtonsoftJson](https://img.shields.io/nuget/v/Badeend.ValueCollections.NewtonsoftJson?label=Badeend.ValueCollections.NewtonsoftJson)](https://www.nuget.org/packages/Badeend.ValueCollections.NewtonsoftJson)

## Basic example

Standalone:

```cs
ValueList<int> a = [1, 2, 3]; // Supports C# 12 collection expressions (even on .NET Framework)
ValueList<int> b = [1, 2, 3];
ValueList<int> c = [1, 2, 3, 4];

Assert(a == b); // Two ValueLists are considered equal when their contents are the same.
Assert(a != c); // Not the same content

// a.Add(42); // Won't compile; ValueLists are immutable.
```

Within a record:

```cs
public record Blog(string Title, ValueList<string> Tags);

var a = new Blog("The Value of Values", ["ddd", "fp"]);
var b = new Blog("The Value of Values", ["ddd", "fp"]);

Assert(a == b); // This would fail if `Tags` was a regular List<T>, ImmutableList<T> or IReadOnlyList<T>.
```

## Constructing new instances

For every immutable ValueCollection type there also exists an accompanying "Builder" type.
```cs
var builder = ValueList.CreateBuilder<int>();

foreach (var x in /* complex source */)
{
    if (/* complex logic */)
    {
        builder.Add(x);
    }
    else if (/* even more logic */)
    {
        builder.Insert(0, x);
    }
    else
    {
        builder.RemoveAll(x);
    }
}

var newList = builder.Build();
```

When constructing ValueCollections, it is generally recommended to use their Builders over e.g. .NET's regular `List<T>`s. The builders are able to take advantage of the immutability of its results and avoid unnecessary copying. Whereas calling `.ToValueList()` on a regular `List<T>` will _always_ perform a full copy. More info [here](./rationale.md).

## Building [fluently](https://en.wikipedia.org/wiki/Fluent_interface)

Many builder methods return `this`, allowing you to chain multiple operations in a single expression.

```cs
ValueList<int> existingList = ...;

var newList = existingList.ToBuilder()
    .Add(4)
    .Add(5)
    .Add(6)
    .RemoveFirst(4)
    .Build();
```

## Boring interface

Being a 100% drop-in replacement for `System.Collections.Generic` is _not_ a goal for this project. Nonetheless, the interface should still feel _very_ familiar:

```cs
ValueList<int> a = ...;
ValueList<int>.Builder b = ...;

/* Reading: */
_ = a.Count;
_ = b.Count;
_ = a[2];
_ = b[2];
_ = a.Contains(42);
_ = b.Contains(42);
_ = a.IndexOf(42);
_ = b.IndexOf(42);
_ = a.LastIndexOf(42);
_ = b.LastIndexOf(42);
_ = a.ToArray();
_ = b.ToArray();
// etc...

/* Writing: */
b[2] = 42;
b.Add(42);
b.AddRange(...);
b.Clear();
b.Insert(42);
b.InsertRange(...);
b.Reverse();
b.Sort();
// etc...
```

[Full API reference](https://badeend.github.io/ValueCollections/api/Badeend.ValueCollections.html)

## Enumeration order

`System.Collections.Generic.HashSet<T>` & `Dictionary<TKey,TValue>` _usually_ preserve the order in which the elements were inserted. Except when they don't...

This behavior is documented, but it's just a single sentence buried in a [huge page](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2):

> The order in which the items are returned is undefined.

Speaking from experience, this is exactly the kind of sneaky caveat that is easy to miss during development and is only discovered "the hard way" when inexplicable things start happening in production.

To raise developer awareness, the [`ValueSet`](xref:Badeend.ValueCollections.ValueSet`1) & [`ValueDictionary`](xref:Badeend.ValueCollections.ValueDictionary`2) types and their respective Builders deliberately **randomize** the enumeration order. If your code breaks because of this, feel free to [buy me a coffee](https://github.com/sponsors/badeend) as I just helped you discover a bug in your code :upside_down_face:

## Other features & omissions

- All immutable types are thread safe. (Pretty much by definition, but still.. :) )
- First-class support for .NET Framework. Even in combination with functionalities not originally present in .NET Framework, such as:
    - Spans
    - C#8 Nullable reference types.
    - C#12 collection expressions.
- Custom `IComparer` parameters are not supported. All operations use the type's `Default(Equality)Comparer`.
- Passes the .NET Runtime's [own testsuite](https://github.com/badeend/ValueCollections/tree/main/Badeend.ValueCollections.Tests/Reference) wherever possible.
