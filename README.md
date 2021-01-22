# Wavefront ASP.NET Core SDK [![travis build status](https://travis-ci.com/wavefrontHQ/wavefront-aspnetcore-sdk-csharp.svg?branch=master)](https://travis-ci.com/wavefrontHQ/wavefront-aspnetcore-sdk-csharp) [![NuGet](https://img.shields.io/nuget/v/Wavefront.AspNetCore.SDK.CSharp.svg)](https://www.nuget.org/packages/Wavefront.AspNetCore.SDK.CSharp)

This SDK collects out of the box metrics, histograms, and (optionally) traces from your ASP.NET Core application and reports the data to Wavefront. Data can be sent to Wavefront using either the [proxy](https://docs.wavefront.com/proxies.html) or [direct ingestion](https://docs.wavefront.com/direct_ingestion.html). You can analyze the data in [Wavefront](https://www.wavefront.com) to better understand how your application is performing in production.

## Installation
Install the [NuGet package](https://www.nuget.org/packages/Wavefront.AspNetCore.SDK.CSharp/).

### Package Manager Console

```
PM> Install-Package Wavefront.AspNetCore.SDK.CSharp
```

### .NET CLI Console

```
> dotnet add package Wavefront.AspNetCore.SDK.CSharp
```

## Configuration
In order to collect HTTP request/response metrics and histograms for your application, you will need to register the Wavefront services that the SDK provides on application startup. This is done in the [`ConfigureServices` method](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/startup?view=aspnetcore-2.1#the-configureservices-method) of the `Startup` class.

The steps to do so are as follows:
1. Create an instance of `ApplicationTags`: metadata about your application.
2. Create an instance of `IWavefrontSender`: low-level interface that handles sending data to Wavefront.
3. Create a `WavefrontAspNetCoreReporter` for reporting ASP.NET Core metrics and histograms to Wavefront.
4. Optionally create a `WavefrontTracer` for reporting trace data to Wavefront.
5. Register Wavefront services in `Startup`. For your ASP.NET Core MVC application, this is done by adding a call to `services.AddWavefrontForMvc()` in `ConfigureServices`.

The sections below detail each of the above steps.

### 1. Set Up Application Tags
Application tags determine the metadata (span tags) that are included with every span reported to Wavefront. These tags enable you to filter and query trace data in Wavefront.

You encapsulate application tags in an `ApplicationTags` object.
See [Instantiating ApplicationTags](https://github.com/wavefrontHQ/wavefront-sdk-doc-sources/blob/master/csharp/applicationtags.md#application-tags) for details.

### 2. Set Up an IWavefrontSender

An `IWavefrontSender` object implements the low-level interface for sending data to Wavefront. You can choose to send data to Wavefront using either the [Wavefront proxy](https://docs.wavefront.com/proxies.html) or [direct ingestion](https://docs.wavefront.com/direct_ingestion.html).

* See [Set Up an IWavefrontSender Instance](https://github.com/wavefrontHQ/wavefront-sdk-doc-sources/blob/master/csharp/wavefrontsender.md#set-up-an-iwavefrontsender-instance) for details on instantiating a proxy or direct ingestion client.

**Note:** If you are using multiple Wavefront C# SDKs, see [Share an IWavefrontSender Instance](https://github.com/wavefrontHQ/wavefront-sdk-doc-sources/blob/master/csharp/wavefrontsender.md#share-an-iwavefrontsender-instance) for information about sharing a single `IWavefrontSender` instance across SDKs.

The `IWavefrontSender` is used by both the `WavefrontAspNetCoreReporter` and the optional `WavefrontTracer`.

### 3. Create a WavefrontAspNetCoreReporter
A `WavefrontAspNetCoreReporter` object reports metrics and histograms to Wavefront.

To build a `WavefrontAspNetCoreReporter`, you must specify:
* An `ApplicationTags` object (see above)
* An `IWavefrontSender` object (see above).

You can optionally specify:
* A nondefault source for the reported data. If you omit the source, the host name is automatically used.
* A nondefault reporting interval, which controls how often data is reported to the IWavefrontSender. The reporting interval determines the timestamps on the data sent to Wavefront. If you omit the reporting interval, data is reported once a minute.

```csharp
// Create WavefrontAspNetCoreReporter.Builder using your ApplicationTags object.
var builder = new WavefrontAspNetCoreReporter.Builder(applicationTags);

// Optionally set a nondefault source name for your metrics and histograms. Omit this statement to use the host name.
builder.WithSource("mySource");

// Optionally change the reporting interval to 30 seconds. Default is 1 minute
builder.ReportingIntervalSeconds(30);

// Create a WavefrontAspNetCoreReporter using your IWavefrontSender object
WavefrontAspNetCoreReporter wfAspNetCoreReporter = builder.Build(wavefrontSender);
```

### 4. Create a WavefrontTracer (Optional)

You can optionally configure a `WavefrontTracer` to create and send trace data from your ASP.NET Core application to Wavefront.

To build a `WavefrontTracer`, you must specify:
* The `ApplicationTags` object (see above).
* A `WavefrontSpanReporter` for reporting trace data to Wavefront. See [Create a WavefrontSpanReporter](https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp#create-a-wavefrontspanreporter) for details.
  **Note:** When you create the `WavefrontSpanReporter`, you should instantiate it with the same source name and `IWavefrontSender` that you used to create the `WavefrontAspNetCoreReporter` (see above).

```csharp
ApplicationTags applicationTags = BuildTags(); // pseudocode; see above
Reporter wavefrontSpanReporter = BuildSpanReporter(); // pseudocode
ITracer tracer = new WavefrontTracer.Builder(wavefrontSpanReporter, applicationTags).Build();
```

### 5. Register Wavefront services
For your ASP.NET Core MVC application, add a call to `services.AddWavefrontForMvc()` in `ConfigureServices` to enable HTTP request/response metrics and histograms for your controller actions.

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Wavefront instrumentation services for ASP.NET Core MVC
        services.AddWavefrontForMvc(wfAspNetCoreReporter);
    }
}
```

Alternatively, if you have optionally configured a `WavefrontTracer` to send trace data, make sure to pass it along as a parameter to ``services.AddWavefrontForMvc()`.

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Wavefront instrumentation services and tracing for ASP.NET Core MVC
        services.AddWavefrontForMvc(wfAspNetCoreReporter, tracer);
    }
}
```

When registered, the `WavefrontTracer` generates server-side trace data for all incoming HTTP requests. It also generates client-side trace data and propagates trace information for all outgoing HTTP requests that use `HttpClientHandler`.

## Metrics, Histograms, and Trace Spans collected from your ASP.NET Core application

See the [metrics documentation](https://github.com/wavefrontHQ/wavefront-aspnetcore-sdk-csharp/blob/master/docs/metrics_mvc.md#metrics-histograms-and-trace-spans-provided-for-aspnet-core-mvc) for details on the out of the box metrics and histograms collected by this SDK and reported to Wavefront.
