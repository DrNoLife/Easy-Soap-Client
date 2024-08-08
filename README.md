# Easy-Soap-Client
Project for talking with SOAP web services, without using a Visual Studio auto-generated proxy

## How to use

Supports Dependency Injection, so to make use of it, add the ```RepositoryFactory``` to the service collection by using either one of these options:

```csharp
builder.Services.AddEasySoapClient();
```

```csharp
builder.Services.AddTransient<IRepositoryFactory, RepositoryFactory>()
```

After doing this, you can inject the factory into your own codebase. Then you need to supply a ```Uri``` for the webservice, this is the base uri for your navision web service, example:

You might have a URL which looks like this: ```https://<domain>/WEBSERVICE/WS/<company>/Pages/WebServiceNameHere``` 
What we need from this is this piece: ```https://<domain>/WEBSERVICE/WS/<company>``` 

After that, you also need to pass in the credentials for the webservice. This is using Basic Auth, meaning username password. To do so, make use of ```ICredentialsProvider``` and the implementation ```Credentials```.

```csharp
Credentials credentials = new("username", "password");
```

After that, combine it together and you can access your newly build repository.

```csharp
var repo = RepositoryFactory.CreateRepository<T>(webserviceUri, credentials);
```

## Create custom models for repository

This project works by creating models which defines namespace, service name, and describes which properties to get data from, from the web service.

Create a class and implement ```IWebServiceElement```, and afterwards use ```[XmlElement]``` to target a Web service property.

Example:

```csharp
public class MachineModel : IWebServiceElement
{
    public string ServiceName => "Machines";
    public string Namespace => "urn:microsoft-dynamics-schemas/page/machines";


    [XmlElement(ElementName = "Key")]
    public string Key { get; set; } = String.Empty;

    [XmlElement(ElementName = "Machine_Name")]
    public string Machine { get; set; } = String.Empty;

    public override string ToString()
    {
        return $"{Machine} [{Key}]";
    }
}
```

This newly build class, is what is used as type parameter for your repositories.

## Filter

Filter the data you get by using the ```ReadMultipleFilter``` struct. Example:

```csharp
ReadMultipleFilter filter = new("ObjectId", "> 100", 20);
```

Which should filter by webservice property ObjectId, and retrieve the first 20 elements that are above 100. 

## Complete code example:

I createed the following example in Blazor, so take that in mind.

Program.cs
```csharp
builder.Services.AddHttpClient();
builder.Services.AddEasySoapClient();
```

Models/MachineModel.cs
```csharp
public class MachineModel : IWebServiceElement
{
    public string ServiceName => "Machines";
    public string Namespace => "urn:microsoft-dynamics-schemas/page/machines";


    [XmlElement(ElementName = "Key")]
    public string Key { get; set; } = String.Empty;

    [XmlElement(ElementName = "Machine_Name")]
    public string Machine { get; set; } = String.Empty;

    public override string ToString()
    {
        return $"{Machine} [{Key}]";
    }
}
```

Components/NavisionComponent.cs
```csharp
@using EasySoapClient.Interfaces
@using BlazorApp1.Models
@using EasySoapClient.Models

@if (_machines is null)
{
    <p>Loading</p>
}
else if (_machines.Count == 0)
{
    <p>No results</p>
}
else
{
    @foreach(var machine in _machines)
    {
        <p>@machine.Machine</p>
    }
}

@code {
    [Inject]
    public IRepositoryFactory RepositoryFactory { get; set; } = default!;

    List<MachineModel>? _machines;

    protected override async Task OnInitializedAsync()
    {
        Uri webserviceUri = new("<WEBSERVICE_URI_HERE>");
        Credentials credentials = new("<USERNAME>", "<PASSWORD>");
        var repo = RepositoryFactory.CreateRepository<MachineModel>(webserviceUri, credentials);
        ReadMultipleFilter filter = new("Machine", "*", 15);
        _machines = await repo.ReadMultipleAsync(filter);
    }
}
```