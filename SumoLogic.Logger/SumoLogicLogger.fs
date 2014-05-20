module SumoLogicLogger

open System
open System.Text
open System.Threading.Tasks
open RestSharp
open Microsoft.Owin
open Microsoft.WindowsAzure.ServiceRuntime

type SumoLogicLogger(next : OwinMiddleware) = 
    inherit OwinMiddleware(next)

    let postToSumoLogic(content : string) =        
        let client = new RestClient()
        client.BaseUrl <- "https://collectors.sumologic.com"
        let request = new RestRequest()
        request.Resource <- RoleEnvironment.GetConfigurationSettingValue "VersionOne.CommitService.SumoLogic.Url"
        request.Method <- Method.POST
        (request.AddFile("Log", Encoding.UTF8.GetBytes(content), "LogFile")) |> ignore
        client.ExecuteTaskAsync(request) |> Async.AwaitTask

    let getCurrentUTCDateAndTime() =
        let now = System.DateTimeOffset.Now.ToUniversalTime()
        let date = now.ToString("MM/dd/yy")
        let time = now.ToString("H:mm:ss")
        date, time
        

    let getLogContent (request:IOwinRequest) (response:IOwinResponse) =
        let success, ua = request.Headers.TryGetValue "User-Agent"
        let userAgent = if success then ua |> Array.reduce(fun acc elem -> acc + elem) else " "
        
        let responseLength = 
            if response.Body.CanSeek then response.Body.Length.ToString() else  " "

        let requestLength = 
            if request.Body.CanSeek then request.Body.Length.ToString() else " "
        
        let date, time = getCurrentUTCDateAndTime()
        
        sprintf "%s, %s, %s, %s, %s, %s, %s, %d, %s, %s" 
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
        let! r = next.Invoke context |> Async.AwaitIAsyncResult
        let content = getLogContent context.Request context.Response
        return! postToSumoLogic content         
    }
                                
    override x.Invoke(context : IOwinContext) = 
        asyncInvoke context |> Async.StartAsTask :> Task