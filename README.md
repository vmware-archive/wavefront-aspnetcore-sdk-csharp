# Wavefront ASP.NET Core SDK

This SDK collects out of the box metrics and histograms from your ASP.NET Core application and reports the data to Wavefront. Data can be sent to Wavefront using either the [proxy](https://docs.wavefront.com/proxies.html) or [direct ingestion](https://docs.wavefront.com/direct_ingestion.html). You can analyze the data in [Wavefront](https://www.wavefront.com) to better understand how your application is performing in production.

## Dependencies
  * .NET Standard (>= 2.0)
  * App.Metrics.AspNetCore (>= 2.0.0)
  * Microsoft.AspNetCore.Mvc (>= 2.1.0)
  * Wavefront.AppMetrics.SDK.CSharp (>= 2.0.0) ([Github repo](https://github.com/wavefrontHQ/wavefront-appmetrics-sdk-csharp/tree/han/refactoring-and-aspnetcore-updates))
  
## Configuration
In order to collect HTTP request/response metrics and histograms for your application, you will need to register the Wavefront services that the SDK provides on application startup. This is done in the [`ConfigureServices` method](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/startup?view=aspnetcore-2.1#the-configureservices-method) of the `Startup` class.

The steps to do so are as follows:
1. Create an instance of `ApplicationTags`: metadata about your application
2. Create an instance of `IWavefrontSender`: low-level interface that handles sending data to Wavefront
3. Create a `WavefrontAspNetCoreReporter` for reporting ASP.NET Core metrics and histograms to Wavefront
4. Register Wavefront services in `Startup`. For your ASP.NET Core MVC application, this is done by adding a call to `services.AddWavefrontForMvc` in `ConfigureServices`

The sections below detail each of the above steps.

### 1. Application Tags
ApplicationTags determine the metadata (aka point tags) that are included with every metric/histogram reported to Wavefront.

See the [documentation](https://github.com/wavefrontHQ/wavefront-sdk-csharp/blob/han/refactoring-and-aspnetcore-updates/docs/apptags.md) for details on instantiating ApplicationTags.

### 2. IWavefrontSender

The `WavefrontAspNetCoreReporter` requires an instance of IWavefrontSender: A low-level interface that knows how to send data to Wavefront. There are two implementations of the Wavefront sender:

* WavefrontProxyClient: To send data to the Wavefront proxy
* WavefrontDirectIngestionClient: To send data to Wavefront using direct ingestion

See the [Wavefront sender documentation](https://github.com/wavefrontHQ/wavefront-sdk-csharp/blob/han/refactoring-and-aspnetcore-updates/README.md#usage) for details on instantiating a proxy or direct ingestion client.

**Note:** When using more than one Wavefront SDK (i.e. wavefront-opentracing-sdk-csharp, wavefront-appmetrics-sdk-csharp, wavefront-aspnetcore-sdk-csharp etc.), you should instantiate the IWavefrontSender only once within the same application instance.

### 3. WavefrontAspNetCoreReporter
To create the `WavefrontAspNetCoreReporter`:

```csharp

// Create WavefrontAspNetCoreReporter.Builder using applicationTags.
var builder = new WavefrontAspNetCoreReporter.Builder(applicationTags);

// Set the source for your metrics and histograms
builder.WithSource("mySource");

// The reporting interval controls how often data is reported to the IWavefrontSender
// and therefore determines the timestamps on the data sent to Wavefront.
// Optionally change the reporting frequency to 30 seconds, defaults to 1 min
builder.ReportingIntervalSeconds(30);

// Create a WavefrontAspNetCoreReporter using the IWavefrontSender instance
var wfAspNetCoreReporter = builder.Build(wavefrontSender);
```

Replace the source `mySource` with a relevant source name.

### 4. Register Wavefront services
For your ASP.NET Core MVC application, add a call to `services.AddWavefrontForMvc` in `ConfigureServices` to enable HTTP request/response metrics and histograms for your controller actions. (This is implemented using an `IResourceFilter`, see the [ASP.NET Core documentation on filters](https://docs.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-2.1#resource-filters) to learn how they work.)

```csharp
public class Startup
{
    ...
    ...
    public void ConfigureServices(IServiceCollection services)
    {
        ...
        // Register Wavefront services for ASP.NET Core MVC using the wfAspNetCoreReporter
        services.AddWavefrontForMvc(wfAspNetCoreReporter);
        ...
    }
    ...
    ...

}
```

## Metrics and Histograms collected from your ASP.NET Core application

See the [metrics documentation](https://github.com/wavefrontHQ/wavefront-aspnetcore-sdk-csharp/blob/han/create-sdk/docs/metrics_mvc.md) for details on the out of the box metrics and histograms collected by this SDK and reported to Wavefront.
