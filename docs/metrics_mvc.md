# Metrics, Histograms, and Trace Spans provided for ASP.Net Core MVC
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

Assume this API is:
1) part of 'Ordering' application 
2) running inside 'Inventory' microservice 
3) deployed in 'us-west-1' cluster 
4) serviced by 'primary' shard 
5) on source = host-1 
6) this API returns HTTP 200 status code

The following metrics and histograms are reported to Wavefront when this API is invoked:

## Request Gauges
|Entity Name| Entity Type|source|application|cluster|service|shard|AspNetCore.resource.controller|AspNetCore.resource.action|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|-----:|-----:|
|AspNetCore.request.inventory.orders.fulfilled.GET.inflight.value|Gauge|host-1|Ordering|us-west-1|Inventory|primary|Inventory|GetAllFulfilledOrders|
|AspNetCore.total_requests.inflight.value|Gauge|host-1|Ordering|us-west-1|Inventory|primary|n/a|n/a|

## Granular Response Metrics
|Entity Name| Entity Type|source|application|cluster|service|shard|AspNetCore.resource.controller|AspNetCore.resource.action|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|-----:|-----:|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.cumulative.count|Counter|host-1|Ordering|us-west-1|Inventory|primary|Inventory|GetAllFulfilledOrders|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.aggregated_per_shard.count|DeltaCounter|wavefront-provided|Ordering|us-west-1|Inventory|primary|Inventory|GetAllFulfilledOrders|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.aggregated_per_service.count|DeltaCounter|wavefront-provided|Ordering|us-west-1|Inventory|n/a|Inventory|GetAllFulfilledOrders|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.aggregated_per_cluster.count|DeltaCounter|wavefront-provided|Ordering|us-west-1|n/a|n/a|Inventory|GetAllFulfilledOrders|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.aggregated_per_appliation.count|DeltaCounter|wavefront-provided|Ordering|n/a|n/a|n/a|Inventory|GetAllFulfilledOrders|

## Granular Response Histograms
|Entity Name| Entity Type|source|application|cluster|service|shard|AspNetCore.resource.controller|AspNetCore.resource.action|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|-----:|-----:|
|AspNetCore.response.inventory.orders.fulfilled.GET.200.latency|WavefrontHistogram|host-1|Ordering|us-west-1|Inventory|primary|Inventory|GetAllFulfilledOrders|

## Completed Response Metrics
This includes all the completed requests that returned a response (i.e. success + errors).

|Entity Name| Entity Type|source|application|cluster|service|shard|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|
|AspNetCore.response.completed.aggregated_per_source.count|Counter|host-1|Ordering|us-west-1|Inventory|primary|
|AspNetCore.response.completed.aggregated_per_shard.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|Inventory|primary|
|AspNetCore.response.completed.aggregated_per_service.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|Inventory|n/a|
|AspNetCore.response.completed.aggregated_per_cluster.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|n/a|n/a|
|AspNetCore.response.completed.aggregated_per_application.count|DeltaCounter|wavefont-provided|Ordering|n/a|n/a|n/a|

## Error Response Metrics
This includes all the completed requests that resulted in an error response (that is HTTP status code of 4xx or 5xx).

|Entity Name| Entity Type|source|application|cluster|service|shard|
| ------------- |:-------------:| -----:|-----:|-----:|-----:|-----:|
|AspNetCore.response.errors.aggregated_per_source.count|Counter|host-1|Ordering|us-west-1|Inventory|primary|
|AspNetCore.response.errors.aggregated_per_shard.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|Inventory|primary|
|AspNetCore.response.errors.aggregated_per_service.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|Inventory|n/a|
|AspNetCore.response.errors.aggregated_per_cluster.count|DeltaCounter|wavefont-provided|Ordering|us-west-1|n/a|n/a|
|AspNetCore.response.errors.aggregated_per_application.count|DeltaCounter|wavefont-provided|Ordering|n/a|n/a|n/a|

## Tracing Spans

Every span will have the controller action name as span name and a start time and duration in milliseconds. Additionally the following attributes are included in the generated tracing spans:

| Span Tag Key          | Span Tag Value                         |
| --------------------- | -------------------------------------- |
| traceId               | 4a3dc181-d4ac-44bc-848b-133bb3811c31   |
| parent                | q908ddfe-4723-40a6-b1d3-1e85b60d9016   |
| followsFrom           | b768ddfe-4723-40a6-b1d3-1e85b60d9016   |
| spanId                | c908ddfe-4723-40a6-b1d3-1e85b60d9016   |
| component             | AspNetCore                             |
| span.kind             | server                                 |
| application           | Ordering                               |
| service               | Inventory                              |
| cluster               | us-west-1                              |
| shard                 | primary                                |
| location              | Oregon (*custom tag)                   |
| env                   | Staging (*custom tag)                  |
| http.method           | GET                                    |
| http.url              | http://{SERVER_ADDR}/orders/fulfilled  |
| http.status_code      | 200                                    |
| error                 | True                                   |
| AspNetCore.path       | /orders/fulfilled                      |
| AspNetCore.resource.controller | Inventory                     |