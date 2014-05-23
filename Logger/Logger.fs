﻿namespace OpenAgile.Azure.SumoLogic.Logger

open System
open System.Text
open System.Threading.Tasks
open RestSharp
open Microsoft.Owin
open Microsoft.WindowsAzure.ServiceRuntime

type Logger(next : OwinMiddleware) = 
    inherit OwinMiddleware(next)

    let log (key : string) (value : System.Object) = System.Diagnostics.Debug.WriteLine("{0}:{1}", key, value)

    let postToSumoLogic(content : string) =        
        let client = new RestClient()
        client.BaseUrl <- RoleEnvironment.GetConfigurationSettingValue "SumoLogic.Url"
        let request = new RestRequest()        
        request.Method <- Method.POST        
        (request.AddFile("ParameterName", Encoding.UTF8.GetBytes(content), "FileName")) |> ignore
        client.ExecuteTaskAsync(request) |> Async.AwaitTask

    let getCurrentUTCDateAndTime() =
        let now = System.DateTimeOffset.Now.ToUniversalTime()
        let date = now.ToString("MM/dd/yy")
        let time = now.ToString("H:mm:ss")
        date, time
        

    let getLogContent (request:IOwinRequest) (response:IOwinResponse) =
        let success, ua = request.Headers.TryGetValue "User-Agent"
        let userAgent =            
            if success then ua |> Array.reduce(fun acc elem -> acc + elem) |> System.Web.HttpUtility.UrlEncode else "-"
        
        let responseLength = 
            if response.ContentLength.HasValue then response.ContentLength.Value.ToString() else  "-"

        let requestLength = 
            if request.Body.CanSeek then request.Body.Length.ToString() else "-"
        
        let date, time = getCurrentUTCDateAndTime()
        
        sprintf "%s %s %s %s %s %s %s %d %s %s" 
            date 
            time 
            request.Method 
            request.Host.Value 
            request.Path.Value 
            request.RemoteIpAddress 
            userAgent 
            response.StatusCode 
            responseLength 
            requestLength

    let asyncInvoke (context:IOwinContext) = async {
        log "Calling next step in the pipeline from sumologic logger." ""
        let! r = next.Invoke context |> Async.AwaitIAsyncResult
        let content = getLogContent context.Request context.Response
        log "Content to post: " content
        return! postToSumoLogic content         
    }
                                
    override x.Invoke(context : IOwinContext) = 
        asyncInvoke context |> Async.StartAsTask :> Task