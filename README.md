# Be.Vlaanderen.Basisregisters.AspNetCore.Mvc.Logging

A filter which logs HTTP calls.
By default logs `POST` and `PUT`.

## Usage

```csharp
public IServiceProvider ConfigureServices(IServiceCollection services)
{
    services
        .AddMvcCore(options =>
        {
            ...
            options.Filters.Add(new LoggingFilterFactory());
            ...
        })
    ...
}
```

```csharp
public IServiceProvider ConfigureServices(IServiceCollection services)
{
    services
        .AddMvcCore(options =>
        {
            ...
            options.Filters.Add(new LoggingFilterFactory(new []{ "GET", "POST", "PUT", "DELETE" }));
            ...
        })
    ...
}
```
