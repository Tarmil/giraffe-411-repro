module testgiraffe411.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.Serialization.Json
open System.Text.Json
open System.Text.Json.Serialization

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "testgiraffe411" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "testgiraffe411" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
            p [] [ a [ _href "/test1" ] [ encodedText "Successful.OK (fails)" ] ]
            p [] [ a [ _href "/test2" ] [ encodedText "Successful.ok (works)" ] ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler (name : string) =
    let greetings = sprintf "Hello %s, from Giraffe!" name
    let model     = { Text = greetings }
    let view      = Views.index model
    htmlView view

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> indexHandler "world"
                routef "/hello/%s" indexHandler
                route "/test1" >=> Successful.OK {| x = 1 |}
                route "/test2" >=> Successful.ok (json {| x = 1 |})
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IHostingEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

type Serializer(o: JsonSerializerOptions) =
    interface IJsonSerializer with
        member _.Deserialize<'T>(string: string) = JsonSerializer.Deserialize<'T>(string, o)
        member _.Deserialize<'T>(bytes: byte[]) = JsonSerializer.Deserialize<'T>(ReadOnlySpan bytes, o)
        member _.DeserializeAsync<'T>(stream) = JsonSerializer.DeserializeAsync<'T>(stream, o).AsTask()
        member _.SerializeToBytes<'T>(value: 'T) = JsonSerializer.SerializeToUtf8Bytes<'T>(value, o)
        member _.SerializeToStreamAsync<'T>(value: 'T) stream = JsonSerializer.SerializeAsync<'T>(stream, value, o)
        member _.SerializeToString<'T>(value: 'T) = JsonSerializer.Serialize<'T>(value, o)

let configureServices (services : IServiceCollection) =
    let options = JsonSerializerOptions()
    options.Converters.Add(JsonFSharpConverter())
    services.AddSingleton(options) |> ignore
    services.AddSingleton<IJsonSerializer, Serializer>() |> ignore

    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddFilter(fun l -> l.Equals LogLevel.Error)
           .AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(contentRoot)
        .UseIISIntegration()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0