---
layout: post
title: Creating custom unobtrusive file extension validation in ASP.NET MVC 3 and
  jQuery
date: '2012-10-25 07:04:00'
tags:
- asp-net-mvc
- javascript
- jquery
---

Just recently I was given the task to move a part of an ASP.NET WebForm to ASP.NET MVC since we are moving over to ASP.NET MVC. The part that I would move over had a form that contained a file upload controller and there were some custom implemented JavaScript hacks to verify that the file had the right file extension. I thought I could do better using data annotations, unobtrusive jQuery and implement a custom validator that also work on the client side so this post is about writing custom validation on the server and unobtrusive validation for ASP.NET MVC 3 on the client.

###Server side validation
The first step to get this working is to get the server side validation to work since you allways want server side validation to make sure the user hasn't bypassed your client side validation. The attribute we need to implement will look something like:

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class FileExtensionsAttribute : ValidationAttribute
    {
        private List<string> ValidExtensions { get; set; }

        public FileExtensionsAttribute(string fileExtensions)
        {
            ValidExtensions = fileExtensions.Split('|').ToList();
        }

        public override bool IsValid(object value)
        {
            HttpPostedFileBase file = value as HttpPostedFileBase;
            if (file != null)
            {
                var fileName = file.FileName;
                var isValidExtension = ValidExtensions.Any(y => fileName.EndsWith(y));
                return isValidExtension;
            }
            return true;
        }
    }

You apply the property like such

    [FileExtensions("txt|doc")]
    public HttPostedFileBase File { get; set; }
	
When this attribute is applied to a property or field of type `HttpPostedFileBase` and is given a string with extensions separated by "|" it will take those extensions and validate the `HttpPostedFileBase` property that it is applied to. As you can see I am returning true if the file is `null`, and that is because if a file should be required should have the `Require` attribute as well. But all this is still on the server and we want to bring it to the client which is the next step.

Before we continue with the next step, which is Unobtrusive client side validation, I thought I would show you a simple editor template to make it easier to get the correct markup for the `HttpPostedFileBase`:

     @model System.Web.HttpPostedFileBase
     @Html.TextBoxFor(model => model, new {type = "file"})

Save the file as HttpPostedFileBase.cshtml in the folder Views/Shared/EditorTemplates. When you have saved the file you will be able to write `@Html.EditorFor(model => model.FileProperty)` and you will get the right html.


###Unobtrusive client side validation
[Unobtrusive JavaScript](http://en.wikipedia.org/wiki/Unobtrusive_JavaScript) is not formally defined but it basically says that you should separate your logic (JavaScript) from your view (the html code) and then bind JavaScript after the page has loaded. This way you don't get any JavaScript code in your view and if you do it correctly the page will still be supported in browsers that don't support JavaScript.

One thing about client side validation on the web is that you should NOT rely on it, it should only be there to make a better user experience. Enough said, let get to the point.

There are four steps that you need to do to implement unobtrusive client side validation: 

 * Enable unobtrusive validation and add the scripts.
 * Implement a `ModelClientValidationRule` that is part of the bridge to the JavaScript.
 * Implement the rule in JavaScript.
 * Implement an adapter that is the second part of the bridge to the JavaScript rule.

#### Enable unobtrusive validation and add the scripts.
This step is the easiest one. You can enable unobtrusive validation in two ways, either in code:

    Html.EnableClientValidation(true);
    Html.EnableUnobtrusiveJavaScript(true);
	
or in web.config:

    <appSettings>
        <add key="ClientValidationEnabled" value="true"/>
        <add key="UnobtrusiveJavaScriptEnabled" value="true"/>
    </appSettings>

**When you have enabled it you need to include the scripts jquery.validate.js, jquery.validate.unobtrusive.js and of course your own script that will contain the custom validation code.**
	
#### Implementing the ModelClientValidationRule
The code for the `ModelClientValidationRule` is: 

    public class ModelClientFileExtensionValidationRule : ModelClientValidationRule
    {
        public ModelClientFileExtensionValidationRule(string errorMessage, List<string> fileExtensions)
        {
            ErrorMessage = errorMessage;
            ValidationType = "fileextensions";
            ValidationParameters.Add("fileextensions", string.Join(",", fileExtensions));
        }
    }
	
It is somewhat straightforward; the `ErrorMessage` is the message that is shown on the client, the `ValidationType` is what it says it is, that is, the type of the validation. When the `ModelClientValidationRule` is done we need to add it to the `ValidationAttribute`:

    public class FileExtensionsAttribute : ValidationAttribute, IClientValidatable
    {
		... // Same as before
		
        public IEnumerable<ModelClientValidationRule> GetClientValidationRules(ModelMetadata metadata, ControllerContext context)
        {
            var rule = new ModelClientFileExtensionValidationRule(ErrorMessage, ValidExtensions);
            yield return rule;
        }
    }
    }

#### Implementing the adapter
The JavaScript adapter is in this case basically the definition of the rule. It doesn't necessary have the actual logic that do the testing, but it sets up everything that needs to be tested. 

    $(function () {
        jQuery.validator.unobtrusive.adapters.add('fileextensions', ['fileextensions'], function (options) {
            // Set up test parameters
    		var params = {
                fileextensions: options.params.fileextensions.split(',')
            };

			// Match parameters to the method to execute
            options.rules['fileextensions'] = params;
            if (options.message) {
			    // If there is a message, set it for the rule
                options.messages['fileextensions'] = options.message;
            }
        });
    } (jQuery));

The code above is executed when the document is ready and adds an "adapter" to the unobtrusive framework. The first paramter is the name of the adapter in this case is "fileextensions" and it should match the `ValidationType` in the `ModelClientFileExtensionValidationRule`. In the `ModelClientFileExtensionValidationRule` we defined a `ValidationParameter` that was named "fileextensions", that is why we have an array with "fileextensions" in it as second parameter. The second parameter should contain the names of all the parameters that are added to the `ValidationParameters` property. The third parameter is the function that basically configures the rule, note that it doesn't execute the rule. 

#### Implementing the rule in JavaScript
Adding a rule to the a test is as simple as adding the adapter. The code below includes both the adapter and the rule.

	$(function () {
		jQuery.validator.unobtrusive.adapters.add('fileextensions', ['fileextensions'], function (options) {
			var params = {
				fileextensions: options.params.fileextensions.split(',')
			};

			options.rules['fileextensions'] = params;
			if (options.message) {
				options.messages['fileextensions'] = options.message;
			}
		});

		jQuery.validator.addMethod("fileextensions", function (value, element, param) {
			var extension = getFileExtension(value);
			var validExtension = $.inArray(extension, param.fileextensions) !== -1;
			return validExtension;
		});

		function getFileExtension(fileName) {
			var extension = (/[.]/.exec(fileName)) ? /[^.]+$/.exec(fileName) : undefined;
			if (extension != undefined) {
				return extension[0];
			}
			return extension;
		};
	} (jQuery));

It is the row `jQuery.validator.addMethod(...)` that is relevant here. We add a rule that we define with the name "fileextensions". The parameters to the rule are the value that is to be validated, the element that is being validated and the parameters that we set up in the adapter. In our case the validation is really straightforward, first we get the file extension using the helper method `getFileExtension`, then we check if the extension is present in the list for file extensions that on the `fileextensions` property on the `param` parameter and return the result. 

I think this is all there is to it. If you have any question or comments just use the form below.