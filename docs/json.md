# JSON

TLDR: install the desired package, call `.AddValueCollections()`, and then you're good to go.

The converters are provided in packages dedicated to each serialization framework.

## System.Text.Json

[![NuGet Badeend.ValueCollections.SystemTextJson](https://img.shields.io/nuget/v/Badeend.ValueCollections.SystemTextJson?label=Badeend.ValueCollections.SystemTextJson)](https://www.nuget.org/packages/Badeend.ValueCollections.SystemTextJson)

```sh
dotnet add package Badeend.ValueCollections.SystemTextJson
```

#### Configure standalone `JsonSerializerOptions`

```cs
using Badeend.ValueCollections.SystemTextJson;

var options = new JsonSerializerOptions();

options.AddValueCollections(); // <--- HERE

var _ = JsonSerializer.Serialize(myObj, options);

```

#### Configure ASP.NET Core

Depending on which parts of ASP.NET Core you use, you might need to configure it twice:

```cs
using Badeend.ValueCollections.SystemTextJson;

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.AddValueCollections(); // <--- HERE
});

// And/or:

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.AddValueCollections(); // <--- HERE
});
```


## Newtonsoft.Json

[![NuGet Badeend.ValueCollections.NewtonsoftJson](https://img.shields.io/nuget/v/Badeend.ValueCollections.NewtonsoftJson?label=Badeend.ValueCollections.NewtonsoftJson)](https://www.nuget.org/packages/Badeend.ValueCollections.NewtonsoftJson)

```sh
dotnet add package Badeend.ValueCollections.NewtonsoftJson
```

#### Configure standalone `JsonSerializerSettings`

```cs
using Badeend.ValueCollections.NewtonsoftJson;

var settings = new JsonSerializerSettings();

settings.AddValueCollections(); // <--- HERE

var _ = JsonConvert.SerializeObject(myObj, settings);

```

#### Configure ASP.NET Core

```cs
using Badeend.ValueCollections.NewtonsoftJson;

services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.AddValueCollections(); // <--- HERE
});
```