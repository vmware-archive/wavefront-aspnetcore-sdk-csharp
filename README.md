# Wavefront ASP.NET Core SDK

This SDK collects out of the box metrics and histograms from your ASP.NET Core application and reports the data to Wavefront. Data can be sent to Wavefront using either the [proxy](https://docs.wavefront.com/proxies.html) or [direct ingestion](https://docs.wavefront.com/direct_ingestion.html). You can analyze the data in [Wavefront](https://www.wavefront.com) to better understand how your application is performing in production.

## Dependencies
  * .NET Standard (>= 2.0)
  * App.Metrics.AspNetCore (>= 2.0.0)
  * Microsoft.AspNetCore.Mvc (>= 2.1.0)
  * Wavefront.AppMetrics.SDK.CSharp (>= 2.0.0) [Github repo](https://github.com/wavefrontHQ/wavefront-appmetrics-sdk-csharp/tree/han/refactoring-and-aspnetcore-updates)
  
## Configuration
In order to collect HTTP request/response metrics and histograms for your application, you will need to register the Wavefront services that the SDK provides on application startup. This is done in the [`ConfigureServices` method](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/startup?view=aspnetcore-2.1#the-configureservices-method) of the `Startup` class.

The steps to do so are as follows:
1. Create an instance of `ApplicationTags`: metadata about your application
2. Create an instance of `IWavefrontSender`: low-level interface that handles sending data to Wavefront
3. Create a `WavefrontAspNetCoreReporter` for reporting ASP.NET Core metrics and histograms to Wavefront
4. Register Wavefront services in `Startup`. For your ASP.NET Core MVC application, this is done by adding a call to `services.AddWavefrontForMvc` in `ConfigureServices`

The sections below detail each of the above steps.
  
## Application Startup
The gathering and reporting of Wavefront metrics is implemented as services that are configured on application startup. The instructions below will show you how to configure the services in the `ConfigureServices` method of the `Startup` class.
  
### 1. Application Tags
The application tags determine the metadata (aka point tags) that are included with the metrics and histograms reported to Wavefront.

The following tags are mandatory:
* `application`: The name of your ASP.NET Core application, for example: `OrderingApp`.
* `service`: The name of the microservice within your application, for example: `inventory`.

The following tags are optional:
* `cluster`: For example: `us-west-2`.
* `shard`: The shard (aka mirror), for example: `secondary`.

You can also optionally add custom tags specific to your application in the form of an `IDictionary` (see example below).

To create the application tags:

```csharp
string application = "OrderingApp";
string service = "inventory";
string cluster = "us-west-2";
string shard = "secondary";

var customTags = new Dictionary<string, string>
{
  { "location", "Oregon" },
  { "env", "Staging" }
};

var applicationTags = new ApplicationTags.Builder(application, service)
    .Cluster(cluster)       // optional
    .Shard(shard)           // optional
    .CustomTags(customTags) // optional
    .Build();
```

You would typically define the above metadata in your application's configuration and create the `ApplicationTags`.

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
For your ASP.NET Core MVC application, add a call to `services.AddWavefrontForMvc` in `ConfigureServices` to enable HTTP request/response metrics and histograms for your controller actions:

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

## Metrics and Histograms provided by this SDK
Let's consider a RESTful HTTP GET API that returns all the fulfilled orders with the controller action below:

```csharp
[ApiController]
public class InventoryController
{
    [Route("orders/fulfilled"]
    [HttpGet]
    public ActionResult<IEnumerable<Order>> GetAllFulfilledOrders() {
       ...
    }
}

```

1) part of 'Ordering' application 
2) running inside 'Inventory' microservice 
3) deployed in 'us-west-1' cluster 
4) serviced by 'primary' shard 
5) on source = host-1 
6) this API returns HTTP 200 status code

When this API is invoked, the following entities (i.e. metrics and histograms) are reported directly from your application to Wavefront.

### Request Gauges
|Entity Name| Entity Type|source|application|cluster|service|shard|AspNetCore.resource.controller|AspNetCore.resource.action|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|-----:|-----:|
|AspNetCore.request.inventory.orders.fulfilled.GET.inflight.value|Gauge|host-1|Ordering|us-west-1|Inventory|primary|Inventory|GetAllFulfilledOrders|
|AspNetCore.total_requests.inflight.value|Gauge|host-1|Ordering|us-west-1|Inventory|primary|n/a|n/a|

### Granular Response Metrics
|Entity Name| Entity Type|source|application|cluster|service|shard|AspNetCore.resource.controller|AspNetCore.resource.action|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|-----:|-----:|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.cumulative.count|Counter|host-1|Ordering|us-west-1|Inventory|primary|Inventory|GetAllFulfilledOrders|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.aggregated_per_shard.count|DeltaCounter|wavefront-provided|Ordering|us-west-1|Inventory|primary|Inventory|GetAllFulfilledOrders|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.aggregated_per_service.count|DeltaCounter|wavefront-provided|Ordering|us-west-1|Inventory|n/a|Inventory|GetAllFulfilledOrders|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.aggregated_per_cluster.count|DeltaCounter|wavefront-provided|Ordering|us-west-1|n/a|n/a|Inventory|GetAllFulfilledOrders|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.aggregated_per_appliation.count|DeltaCounter|wavefront-provided|Ordering|n/a|n/a|n/a|Inventory|GetAllFulfilledOrders|

### Granular Response Histograms
|Entity Name| Entity Type|source|application|cluster|service|shard|AspNetCore.resource.controller|AspNetCore.resource.action|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|-----:|-----:|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.latency|WavefrontHistogram|host-1|Ordering|us-west-1|Inventory|primary|Inventory|GetAllFulfilledOrders|

### Completed Response Metrics
This includes all the completed requests that returned a response (i.e. success + errors).

|Entity Name| Entity Type|source|application|cluster|service|shard|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|
|AspNetCore.response.completed.aggregated_per_source.count|Counter|host-1|Ordering|us-west-1|Inventory|primary|
|AspNetCore.response.completed.aggregated_per_shard.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|Inventory|primary|
|AspNetCore.response.completed.aggregated_per_service.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|Inventory|n/a|
|AspNetCore.response.completed.aggregated_per_cluster.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|n/a|n/a|
|AspNetCore.response.completed.aggregated_per_application.count|DeltaCounter|wavefont-provided|Ordering|n/a|n/a|n/a|

### Error Response Metrics
This includes all the completed requests that resulted in an error response (that is HTTP status code of 4xx or 5xx).

|Entity Name| Entity Type|source|application|cluster|service|shard|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|
|AspNetCore.response.errors.aggregated_per_source.count|Counter|host-1|Ordering|us-west-1|Inventory|primary|
|AspNetCore.response.errors.aggregated_per_shard.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|Inventory|primary|
|AspNetCore.response.errors.aggregated_per_service.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|Inventory|n/a|
|AspNetCore.response.errors.aggregated_per_cluster.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|n/a|n/a|
|AspNetCore.response.errors.aggregated_per_application.count|DeltaCounter|wavefont-provided|Ordering|n/a|n/a|n/a|
