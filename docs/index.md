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

> Preface: The examples in this document focus on ValueLists, but the same principles apply equally to [ValueSets](https://badeend.github.io/ValueCollections/api/Badeend.ValueCollections.ValueSet-1.html) and ValueDictionaries.

## Installation

[![NuGet Version](https://img.shields.io/nuget/v/Badeend.ValueCollections)](https://www.nuget.org/packages/Badeend.ValueCollections)

```
dotnet add package Badeend.ValueCollections
```

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

var a = new Blog("The Value of Values", ["ddd", "sanity"]);
var b = new Blog("The Value of Values", ["ddd", "sanity"]);

Assert(a == b); // This would fail if `Tags` was a regular List<T>, ImmutableList<T> or IReadOnlyList<T>.
```

## Constructing new instances

For every immutable ValueCollection type there also exists an accompanying "Builder" type.
```cs
var builder = new ValueListBuilder<int>(); // Or: ValueList.Builder<int>()

foreach (var x in /* complex source */)
{
    if (/* complex logic */)
    {
        builder.Add(x);
    }
    else
    {
        builder.Remove(x);
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
    .Remove()
    .Build();
```

## Boring interface

Despite being a 100% drop-in replacement for `System.Collections.Generic` is _not_ a goal for this project, the interface should still feel _very_ familiar:

```cs
ValueList<int> a = ...;
ValueListBuilder<int> b = ...;

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

## Other features & omissions

- All immutable types are thread safe. (Pretty much by definition, but still.. :) )
- First-class support for .NET Framework. Even for functionalities not originally present in .NET Framework, such as:
    - Spans
    - C#8 Nullable reference types.
    - C#12 collection expressions.
- Custom `IComparer` parameters are not supported. All operations use the type's `Default(Equality)Comparer`.
- Passes the .NET Runtime's [own testsuite](https://github.com/badeend/ValueCollections/tree/main/Badeend.ValueCollections.Tests/Reference) wherever possible.
