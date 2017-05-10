---
layout: post
title: Configuring Windows Azure Access Control Service
date: '2013-04-28 07:37:00'
tags:
- security
- net
- azure
---

When implementing an application I think it is nice if you can focus your development solving the problems the application should solve and almost nothing else. Most of the time authentication isn't really part of what your application should solve so if you can let someone else handling that part for you, and it would be really great if it were someone you trust.

Configuring Windows Azure Control Service, also known as Windows Azure Active Directory Control or ACS as I will call it through out this post, is not that hard to do as long as you know how to do it. However, a lot of the documentation out there is outdated (things move fast in this space) so I thought I would write this... which will most likely be outdated in a couple of days or so :). The walkthrough will be made using the latest version of .NET, that is, .NET 4.5 as of the moment.

## How it works
When you authenticate against an application that uses Azure ACS it is some kind of table tennis game that goes on behind the curtains to identify you. This is a good thing because all you need to have in mind as a developer is the Security Token Service or STS. The STS in our case is Acure ACS and it is then responsible for coordinating everything with the other STS out there as google and microsoft live.

The table tennis going is illustrated below:
![ACS authentication flow][1]

 1. The client to access some kind of resource from the application, also often mentioned as a Relying Party (RP).
 2. The application answer with a not authorize request and the clients is redirected to the access control site.
 3. At the access control site the client are presented with some different options that can be used to authenticate, as google or Microsoft Live for example.
 4. The client choose one of the identity providers and are redirected to the site where it authenticate authenticate.
 5. The client gets a token from the identity provider identifying the client.
 6. The client presents the token to the ACS and the ACS gives another token that should be used to authenticate the client.
 7. The client shows the token to the application and the authentication process is done.


There are some trust that has to be established for this to work. First the application have to trust the access control and the access control has to trust the identity providers. In azure you can configure the urls of the application, that is, establishing the trust between the application and acs. You can also define which identity providers to use, that is you define that you trust them.

## Configuring azure

 1. Create the access control namespace
![Create namespace][2]

 2. Choose your namespace and click "manage", this will take you to the management portal for access control
![Go to navigate][3]

 3. Choose the identity providers you want to support
![enter image description here][4]

 4. Add the relying party, which is basically which url you want to connect to the access control. Enter data in the `Realm` and `Return URL`
![Relying party configuration][5]

 5. Click to edit the rule group that was created for your relying party and click generate to ![enter image description here][6]

 6. The following three images shows you how to get to the management key which you will need later
![Management service][7]
![Management service symmetric key][8]
![Management service getting the key][9]

That's all you need to do in azure. Copy or write down the key that you located in the last step so you can use it later.

## Configuring your application
Before starting Visual Studio install the [Identity and Access Tool](http://visualstudiogallery.msdn.microsoft.com/e21bf653-dfe1-4d81-b3d3-795cb104066e) which will give you some help configuring your application to use the Azure ACS. To demonstrate how it works I will guide you through how to require authentication and list the claims received from the authentication.

 1. Create an empty web application.
 2. Add a reference to `System.IdentityModel.Services`, which you will have if are running .Net 4.5.
 3. Right click on the web project and click the `Identity and Access...` option which is now available since you installed the Identity and Access Tool.
 ![Identity and Access option][10]
 4. Check the "Use the Windows Azure Access Control Service" and click "(Configure...)"
 ![Configure Access Control][11]
 5. In the "Configure ACS namespace" enter the name of your acs namespace, which you created in the first step configuring Azure, and enter the management key you retrieved in the last step in the field for the key.
![Configure ACS namespace][12]

 6. In the `system.web` section in the web.config add the following

    <authorization>
      <deny users="?" />
    </authorization>

After the steps above you have an updated web.config that enables you to use the ACS to authenticate but the application is still empty so we will implement a simple application that list all the claims retrieved when you have authenticated against Azure ACS in two simple steps:

 1. So first add a `HomeController` with the following simple `Index` method:

    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }

 2. Add the `Index` view:

        @using System.Security.Claims
        @using System.Threading
        @{
            var claimsIdentity = Thread.CurrentPrincipal.Identity as ClaimsIdentity;
        }
        <h2>Amazing claims:</h2>
        <ul>
        @foreach (var claim in claimsIdentity.Claims)
        {
            <li>
                <span class="key">Type: @claim.Type</span>, 
                <span class="value">Value: @claim.Value</span>
            </li>  
        }
        </ul>


Now hit F5 and hopefully you will be presented a view telling you to authenticate with google or Microsoft Live. After you have authenticated all the claims retreived are listed on the page.

## Getting the application to run on Azure
There are three things you need to do to get the application:

 1. Add a relying party for your application.
 2. Add the following web.config transform to change the realm:

        <system.identityModel>
            <identityConfiguration>
                <audienceUris>
                    <add value="http://yourapp/" xdt:Transform="Replace"/>
                </audienceUris>
            </identityConfiguration>
        </system.identityModel>
        <system.identityModel.services>
            <federationConfiguration>
                <wsFederation realm="http://yourapp/" xdt:Transform="SetAttributes(realm)" />
            </federationConfiguration>
        </system.identityModel.services>

 3. Create a Windows Azure Clour Service project and deploy your application.

That was all for now and hope the description is correct.

  [1]: https://vpm6ug.blu.livefilestore.com/y1pVRlI1m7jv1E0xMc1tH8QWUXNAquFCa8jQB1f3061QzmGPOoLtqJtfQUSK3VdTIIdyFhEjJG1TTZkjiSDDFqCIED_7uqEBMR7/ACS_flow.png?psid=1
  [2]: https://qbtmcq.blu.livefilestore.com/y1pEN61EJrZ-ljnAdi9ThVij98kFuNZ2mJgYc5zJppJPtQmQFWf9byp_aZgYHHN3heJHHhx2lHODpVNciyRcIr0H-sUbRLvxGI4/ACS_Azure1.png?psid=1 "Create namespace"
  [3]: https://qbtmcq.blu.livefilestore.com/y1p2LEuZwCar7Vu3XOaMipciEMx0-cCap6ha4EneKtHgI1ENjRVtCGrI2yABg0ar-pxPOxgCFJ-9c1Y4OoGevIc8p-lWW4Rjg8_/ACS_Azure2.png?psid=1 "Go to navigate"
  [4]: https://qbtmcq.blu.livefilestore.com/y1p3VfAUpOvTuflmORgwLs3z8j3iWHsn_hAokLt76OkDvzxFcACoy6-nnALqDKwzGjJZHe3rmuZyFH_OBDkv5zYlKfz4tKv91eJ/ACS_Azure3.png?psid=1 "Choose identity providers"
  [5]: https://qbtmcq.blu.livefilestore.com/y1pnNN6uqnWDjY1bmPZGi-KB0fXLKJsT0GkK8f0Q2O8qnNpTrfkiNEdrnKM4n8QJ2X70tqWHB9L8DEsOpIBuhqLBW_Odb03_Rbe/ACS_Azure4.png?psid=1 "Relying party"
  [6]: https://qbtmcq.blu.livefilestore.com/y1pog5hQp31PaFGYQ7TetjLQelLR65BoWStuq3_JhG0uBZ7O9HHrkTyVFMb3XcJRBGvzdVYYsyiAbx5D_7zWJJxWFWvDla170hj/ACS_Azure5.png?psid=1 "Rule group configuration"
  [7]: https://qbtmcq.blu.livefilestore.com/y1pAROgKMCyjpGHSoQqROIZ0Lh6vUF3ABJirpvEgeWaCG93X8qdrEkbNYnGG5_rRsfFzNfzsHS8v2JlNE-c9eSLLTssCYYKu6Si/ACS_Azure6.png?psid=1
  [8]: https://qbtmcq.blu.livefilestore.com/y1pAbToQkhuTS5Rn-uEKAx-cdZkAaJRPQPZ5Jqn_KIqCOL-LBa76uY3qamo6T0u2aI66h_YXT4Xrw1LeoaTRcGYYHrgOKAFswbb/ACS_Azure7.png?psid=1 "Management service symmetric key"
  [9]: https://qbtmcq.blu.livefilestore.com/y1phuCt4lLPSWVGw8W_C6vWa889UmJzQZS-89e9Wi3t25yN_RHJtiKz000CD-80J_kWdWUXqjVRDCbIfp7TfC6cRYMY93TyGpN0/ACS_Azure8.png?psid=1 "Edit Management service"
  [10]: https://qbtmcq.blu.livefilestore.com/y1p8fMIToSsRCx9rzZKYG24MHyhwySNZQXceY-1VepSbs8tyvYIq1XxTbNFggBjHJr2sPWNPVzSMH45TK11WwXUV59AAcLjR9sj/ACS_VS1.png?psid=1 "Identity and Access option"
  [11]: https://qbtmcq.blu.livefilestore.com/y1pm7SOOPUBoMQi-mnayetcgY1meCH_uKRD-7Sr0NRRdtb2yUutdWvVqOis_bt1-la5bhK4DFtjOAM6bsLpcKrozWnT41V2Glqu/ACS_VS2.png?psid=1 "Configure Access Control"
  [12]: https://qbtmcq.blu.livefilestore.com/y1p84U3Fe8bE03Hkl0UddGeSOTLJiK8t9Uz3Ik_Weu5cDa6HTGxnxtN_wMychIct_UwcblUet8QdF7uKlhKjVABVVZg-2U1xM3D/ACS_VS3.png?psid=1 "Configure ACS namespace"