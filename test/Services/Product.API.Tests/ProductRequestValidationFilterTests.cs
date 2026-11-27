using FluentValidation;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Product.API.Contracts.V1;
using Product.API.Options;
using Product.API.Validation;

namespace Product.API.Tests;

public sealed class ProductRequestValidationFilterTests
{
    [Theory]
    [InlineData("valid")] [InlineData("invalid_catalog")] [InlineData("invalid_image")]
    [InlineData("no_validator")] [InlineData("null_argument")]
    public async Task Validation_filter_checks_arguments_before_invoking_action(string scenario)
    {
        var services=new ServiceCollection();
        services.AddSingleton<IValidator<CreateCategoryRequest>,CreateCategoryRequestValidator>();
        services.AddSingleton<IValidator<CreateProductImageUploadRequest>>(new CreateProductImageUploadRequestValidator(Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions())));
        using var provider=services.BuildServiceProvider();
        var http=new DefaultHttpContext{RequestServices=provider};
        var action=new ActionContext(http,new RouteData(),new ActionDescriptor(),new ModelStateDictionary());
        object? argument=scenario switch {
            "valid"=>new CreateCategoryRequest{Name="Name",Slug="slug",SortOrder=0},
            "invalid_catalog"=>new CreateCategoryRequest(),"invalid_image"=>new CreateProductImageUploadRequest(),
            "null_argument"=>null,_=>"no validator for strings"
        };
        var context=new ActionExecutingContext(action,[],new Dictionary<string,object?>{["request"]=argument},new object());
        var called=false;
        Task Next()=>new ProductRequestValidationFilter().OnActionExecutionAsync(context,()=>{
            called=true;return Task.FromResult(new ActionExecutedContext(action,[],context.Controller));
        });
        if(scenario.StartsWith("invalid")){
            var error=await Assert.ThrowsAsync<Infrastructure.Exceptions.ValidationException>(Next);Assert.Equal("VALIDATION_FAILED",error.Code);Assert.False(called);
        }
        else{await Next();Assert.True(called);}
    }
}
