open System
open System.IO
open System.Text.RegularExpressions

let split (splitOn:char) (str:String) = 
    str.Split(splitOn) |> List.ofArray

let getFiles folder = 
    Directory.GetFiles(folder)

let readFile file = 
    file, File.ReadAllText(file)

let inlinePattern = """!\[.+\]\((.+)\)"""
let referencePattern = """!\[.+\]\[(\d+)\]"""

let referenceLinkPattern refNo = sprintf "\\[%s\\]\\:\\s(.+)$" refNo

let matchLinks pattern content = 
    let regex = new Regex(pattern)
    let matches = regex.Matches(content)
    let en = matches.GetEnumerator()
    
    let rec loop() = seq {
        if en.MoveNext() 
        then 
            yield (en.Current :?> Match).Groups.Item(1).ToString()
            yield! loop() }
    loop()

let findLinks strategy (file, content) = 
    strategy content
    |> Seq.toList
    |> List.map (fun m -> file, m)

let matchInlineLinks = matchLinks inlinePattern

let matchFooterLinks content = 
    matchLinks referencePattern content 
    |> Seq.map (fun refStr -> matchLinks (referenceLinkPattern refStr) content)
    |> Seq.collect id

let findImageLinks input = 
    input
    |> findLinks matchInlineLinks
    |> List.append (
        input
        |> findLinks matchFooterLinks
    )

open System.IO
open System.Net
let downloadFile (url:string) (fileName:string) = 
    let url = 
        if url.StartsWith("http") then url
        else "http://blog.2mas.xyz" + url
    try
        printfn "Downloading: %s" url
        let wc = new WebClient()
        wc.DownloadFile(url, "_images/migrated/" + fileName)
    with
    | _ ->
        printfn "Failed to download: %s" url
        ()

let result = 
    getFiles "_posts"
    |> Seq.map (readFile >> findImageLinks)
    |> Seq.collect id
    |> Seq.toList

result |> (printfn "====> %A")

result
    |> List.map (fun (f, l) -> (f,l, (l |> split '/' |> List.last |> split '?' |> List.head)))
    |> List.map (fun (f, l, n) -> downloadFile l n; (f,l,n))

let sample = """
Illustration of the flow from code to deployment:

![Owin pipeline][1]

![The build deploy process](/content/images/2015/10/CodeToDeploy.JPG)

![The build deploy process](/content/images/2015/10/CodeToDeploy.JPG)
I think the end result is really good since it is the first time I used FAKEand AppVeyor. If you want to see the sample 
![The build deploy process](/content/images/2015/10/CodeToDeploy.JPG)

  [1]: https://qbtmcq.dm2302.livefilestore.com/y2pCgbiQedecsgVAzavyDd0MHMy5oIqWUayyuWMX2u119bfjXdyuXqiom8p90qhUA9rsOsVUqT8rQOz5GT4ZCilMIgAUmhSkEWfcB_MAig2uNA/SimpleMiddlewarePipeline.png?psid=1
"""

//("test", sample) |> findImageLinks |> printfn "==> %A"