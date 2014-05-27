namespace OpenAgile.Azure.SumoLogic.Logger

open System
open System.Text
open System.IO
open System.Threading.Tasks
open RestSharp
open System.Collections.Generic
open System.Linq

type appFunc = Func<IDictionary<string,Object>,Task>

type Logger(next:appFunc, sumoLogicUrl:string) =    
    
    let getCurrentUTCDateAndTime() =
        let now = System.DateTimeOffset.Now.ToUniversalTime()
        let date = now.ToString("MM/dd/yy")
        let time = now.ToString("H:mm:ss")
        date, time
        
    let getUserAgent (headers: IDictionary<string, string[]>) =
        let success, ua = headers.TryGetValue "User-Agent"
        if success then ua |> Array.reduce(fun acc elem -> acc + elem) |> System.Web.HttpUtility.UrlEncode else "-"

    let getContentLength (headers: IDictionary<string, string[]>) =
        let success, length = headers.TryGetValue "Content-Length"        
        if success then length.First() else "0"

    let getStreamLength (stream:Stream) = if stream.CanSeek then stream.Length.ToString() else "-"

    let getLogContent (environment:IDictionary<string,Object>) =        
        let userAgent = environment.["owin.RequestHeaders"] :?> IDictionary<string, string[]> |> getUserAgent
        let requestLength = environment.["owin.RequestBody"] :?> Stream |> getStreamLength
        let responseLength = environment.["owin.ResponseHeaders"] :?> IDictionary<string, string[]> |> getContentLength
        let requestMethod = environment.["owin.RequestMethod"] :?> String
        let requestPath = environment.["owin.RequestPath"] :?> String
        let requestPathBase = environment.["owin.RequestPathBase"] :?> String
        let remoteIpAddress = environment.["server.RemoteIpAddress"] :?> String
        let localIpAddress = environment.["server.LocalIpAddress"] :?> String        
        let responseStatusCode = environment.["owin.ResponseStatusCode"].ToString()                                               
        let date, time = getCurrentUTCDateAndTime()
        
        sprintf "%s %s %s %s %s %s %s %s %s %s"
            date 
            time 
            requestMethod 
            localIpAddress
            requestPath 
            remoteIpAddress
            userAgent 
            responseStatusCode 
            responseLength 
            requestLength


    member x.Next = next
    member x.SumoLogicUrl = sumoLogicUrl

    member x.PostToSumoLogic(content : string) =        
        let client = new RestClient()
        client.BaseUrl <- x.SumoLogicUrl
        client.UserAgent <- "Openagile.Sumologic.Logger"
        let request = new RestRequest()        
        request.Method <- Method.POST        
        (request.AddFile("ParameterName", Encoding.UTF8.GetBytes(content), "FileName")) |> ignore
        client.ExecuteTaskAsync(request) |> Async.AwaitTask
        
    member x.AsyncInvoke (environment:IDictionary<string,Object>) = async {
        let! r = x.Next.Invoke environment |> Async.AwaitIAsyncResult        
        let content = getLogContent environment
        return! x.PostToSumoLogic content         
    }
    
    member x.Invoke(environment :IDictionary<string,Object>) = 
        x.AsyncInvoke environment |> Async.StartAsTask :> Task