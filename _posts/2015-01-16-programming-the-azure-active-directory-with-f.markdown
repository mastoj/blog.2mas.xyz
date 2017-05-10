---
layout: post
title: Programming the Azure Active Directory with F#
date: '2015-01-16 18:47:07'
tags:
- azure
- fsharp
- azure-active-directory
---

I've been playing around with both Azure and F# lately and thought that I would share my knowledge about programming the Azure Active Directory using F#. It took some time to find some good example code, but after some searching I did find this project: https://github.com/AzureADSamples/ConsoleApp-GraphAPI-DotNet. The project shows most of the basic functionality, but I thought I would show it in F# and at the same time try to make it easier for others to understand. So let's started.

##TL;DR;
I've put the code on github, https://github.com/mastoj/ADTutorial, so you can download it easier. This will get you started quite easy and manipulating Active Directory in no time at all. When working with this short post/tutorial I had some issues with connecting to the AD's to start with, but the problem just went a way. My guess is that it might have taken some time from the creation of the Active Directory and when it was accessible by code, but that's just a guess.

##Creating the Active Directory in Azure
We will create a new directory that we can play with. So to create the directory choose "CUSTOM CREATE" that is available after following the onscreen menu in the picture below.
![Create Active Directory](/content/images/2015/01/CreateAD.PNG)

Enter some information in the "Add directory" (sample data below).
![Add directory](/content/images/2015/01/AddDirectory.PNG)

In the active directory list click the arrow for your new directory to get to the details of this directory.
![Active directory list](/content/images/2015/01/ADList.PNG)

To connect to the Active Directory, which are going to do through a console application, we need to add an application to the Active Directory. So go to the "Application" tab and then click "ADD" at the bottom of the screen. In the dialogue choose "Add an application my organization is developing" and then "WEB APPLICATION AND/OR WEB API", as "REDIRECT URI" and just choose a unique for "APP ID URI" use "http://localhost".
![Add application](/content/images/2015/01/AddApplication-1.PNG)

##Collecting and configuring the data needed to connect
For the things we are going to do later there are some data we need to collect to be able to connect to the active directory. The things we need are:

 * Client Id
 * Client secret
 * Tenant name
 * Tenant id

I had problem finding this information, so I'll try to make it as clear as I can. Before we locate those for things there is also some other configuration settings which is more static. 

 * Resource url (url to the resource we acces to configure AD): https://graph.windows.net
 * Authentication string: https://login.windows.net/<Tenant name>
 
The last thing we need to do is configure the permissions of the application so we can access the active directory.

### The client id
The client id is found under the "CONFIGURE" tab.
![Client id](/content/images/2015/01/ClientId.PNG)

### The client secret
This wasn't as straightforward as the id, but with secret they often mean "key". So on the "CONFIGURE" tab we need to generate a 1 or 2 year key that we can use. The key will only be visible after you save and only that time.
![Client secret](/content/images/2015/01/ClientSecret.PNG)

### The tenant name and id
The name is the easiest part, if you haven't got any custom domain names this is the domain name you used for the "Active Directory". So in this example it is "fsharptest.onmicrosoft.com" (no http before the name).

The id is a little bit harder to find. There are probably many ways to find the id, one is to visit the url: https://login.windows.net/fsharptest.onmicrosoft.com/FederationMetadata/2007-06/FederationMetadata.xml, but change my domain with yours. The tenant id is the guid in the "entityId" url on the first node in the document. 

Another way to find the tenant id is to expand the "ENABLE USERS TO SIGN ON" and there it is in the magic url:
![Tenant id](/content/images/2015/01/TenantId.PNG)

##Time to code
Since I do prefer F# when I can the coding part will be done in F#, but you can easily translate the code to C# if you want to. I'll write everything in one file in a console app. To get started the easiest way is to use to nuget packages:

 * `Microsoft.IdentityModel.Clients.ActiveDirectory` (I used version 2.13.112191810)
 * `Microsoft.Azure.ActiveDirectory.GraphClient` (I used version 2.0.3)

The first part of the files are the settings, I like to keep those in a separate module:

    [<AutoOpen>]
    module AdSettings = 
        let clientId = "the client id guid"
        let clientSecret = "the key from azure"
        let tenantId = "the tenant id"
        let tenantName = "the domain"
        let resourceUrl = "https://graph.windows.net"
        let authString = "https://login.windows.net/" + tenantName

Then we have the actual client code, the code is not intended for production use I'm just showing of the AD integration.

    [<AutoOpen>]
    module AdClient = 
        open Microsoft.Azure.ActiveDirectory.GraphClient
        open Microsoft.IdentityModel.Clients.ActiveDirectory
        open System
        open System.Linq.Expressions
        open System.Threading.Tasks
        open Microsoft.FSharp.Linq.RuntimeHelpers
    
        let getAuthenticationToken (clientId:string) (clientSecret:string) tenantName = 
            let authenticationContext = AuthenticationContext(authString, false)
            let credentials = ClientCredential(clientId, clientSecret)
            let authenticationResult = authenticationContext.AcquireToken(resourceUrl, credentials)
            authenticationResult.AccessToken

        let activeDirectoryClient (tenantId:string) token = 
            let serviceRoot = Uri(resourceUrl + "/" + tenantId)
            let tokenTask = Func<Task<string>>(fun() ->Task.Factory.StartNew<string>(fun() -> token))
            let activeDirectoryClient = ActiveDirectoryClient(serviceRoot, tokenTask)
            activeDirectoryClient

        let client = getAuthenticationToken clientId clientSecret tenantName |> activeDirectoryClient tenantId

        let toExpression<'a> quotationExpression = quotationExpression |> LeafExpressionConverter.QuotationToExpression |> unbox<Expression<'a>>

        let getGroup groupName = 
            let matchExpression = <@Func<IGroup,bool>(fun (group:IGroup) -> group.DisplayName = groupName) @>
            let filter = toExpression<Func<IGroup,bool>> matchExpression
            let groups = client
                            .Groups
                            .Where(filter)
                            .ExecuteAsync()
                            .Result
                            .CurrentPage
                            |> List.ofSeq
            match groups with
            | [] -> None
            | x::[] -> Some (x :?> Group)
            | _ -> raise (Exception("more than one group exists"))

        let addGroup groupName = 
            match getGroup groupName with
            | None ->
                let group = Group()
                group.DisplayName <- groupName
                group.Description <- groupName
                group.MailNickname <- groupName
                group.MailEnabled <- Nullable(false)
                group.SecurityEnabled <- Nullable(true)
                client.Groups.AddGroupAsync(group).Wait()
                Some group
            | Some x -> Some (x :?> Group)

        let getUser userName = 
            let matchExpression = <@Func<IUser,bool>(fun (user:IUser) -> user.DisplayName = userName) @>
            let filter = toExpression<Func<IUser,bool>> matchExpression
            let users = client
                            .Users
                            .Where(filter)
                            .ExecuteAsync()
                            .Result
                            .CurrentPage
                            |> List.ofSeq
            match users with
            | [] -> None
            | x::[] -> Some x
            | _ -> raise (Exception("more than one user exists with that name"))

        let addUser userName = 
            match getUser userName with
            | None ->
                let passwordProfile() =
                    let passwd = PasswordProfile()
                    passwd.ForceChangePasswordNextLogin <- Nullable(true)
                    passwd.Password <- "Ch@ng3NoW!"
                    passwd
                let user = User()
                user.PasswordProfile <- passwordProfile()
                user.DisplayName <- userName
                user.UserPrincipalName <- userName + "@fsharptest.onmicrosoft.com"
                user.AccountEnabled <- Nullable(true)
                user.MailNickname <- userName
                client.Users.AddUserAsync(user).Wait()
                Some user
            | Some x -> Some (x :?> User)

        let getMembers (group:Group) = 
            let groupFetcher = (group :> IGroupFetcher)
            let members = groupFetcher.Members.ExecuteAsync().Result
            members.CurrentPage

        let groupContainsUser (group:Group) (user:User) = 
            group |> getMembers |> Seq.map (fun o -> (o :?> User).DisplayName) |> Seq.exists (fun s -> s = user.DisplayName)

        let addUserToGroup (group:Group) (user:User) = 
            match groupContainsUser (group:Group) (user:User) with
            | false ->
                group.Members.Add(user)
                group.UpdateAsync().Wait()
                group
            | true ->
                group

The client code is sort of straightforward. The first part, down to the `client` declaration is just about connecting to the API. The names of the functions after that explains them self. To do a query we need to create an expression which we do by creating a code quotation which we translate to a `Func`. The code here is not by any means what you should have in production, but it show you some part of the API and what you can do.

The last part is a small program that uses the module we just created:

    open System
    open Microsoft.Azure.ActiveDirectory.GraphClient
    [<EntryPoint>]
    let main argv = 
        try
            let group = addGroup "newgroup" |> Option.get
            let user = addUser "charlie" |> Option.get
            user |> addUserToGroup group |> ignore
            let group2 = getGroup "newgroup" |> Option.get

            printfn "Group name: %s" group2.DisplayName
            let membersOfGroup = getMembers group
            let members = membersOfGroup |> Seq.map (fun o -> (o :?> User).DisplayName) |> String.concat ", "
            printfn "Members: %s" members
            Console.ReadLine() |> ignore
        with
        | :? Exception as ex -> printfn "%s" ex.Message
        0 
