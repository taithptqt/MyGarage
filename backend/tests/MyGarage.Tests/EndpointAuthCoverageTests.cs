using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace MyGarage.Tests;

public class EndpointAuthCoverageTests
{
    private static readonly HashSet<string> ReviewedAnonymousActions = new(StringComparer.Ordinal)
    {
        "AppointmentsController.CreateGuest",
        "AppointmentsController.GetAvailability",
        "AuthController.ForgotPassword",
        "AuthController.GetSession",
        "AuthController.GoogleLogin",
        "AuthController.Login",
        "AuthController.Logout",
        "AuthController.Register",
        "AuthController.ResetPassword",
        "BranchesController.GetAll",
        "BranchesController.GetById",
        "BranchExceptionsController.GetAll",
        "BranchExceptionsController.GetById",
        "BranchOperatingHoursController.GetAll",
        "BranchOperatingHoursController.GetById",
        "CategoriesController.GetAll",
        "CategoriesController.GetById",
        "PackagesController.GetAll",
        "PackagesController.GetById",
        "ProductImageController.GetAll",
        "ProductImageController.GetById",
        "ProductsController.GetAll",
        "ProductsController.GetByBranch",
        "ProductsController.GetById",
        "ProductsController.GetPublicCatalog",
        "ProductsController.GetRetailCatalog",
        "ProductsController.GetRetailStockByBranches",
        "ProductsController.GetStockAvailability",
        "QuotationController.ApplyDecisionByPublicToken",
        "QuotationController.GetByPublicToken",
        "QuotationController.SendDecisionOtpByPublicToken",
        "ServiceCategoriesController.GetAll",
        "ServiceCategoriesController.GetById",
        "StandardServicesController.GetAll",
        "StandardServicesController.GetById",
        "VehicleBrandsController.GetAll",
        "VehicleBrandsController.GetById",
        "VehicleModelsController.GetAll",
        "VehicleModelsController.GetById",
        "WebhookController.VerifyPayment"
    };

    [Fact]
    public void EveryControllerAction_MustHaveAuthorizeOrReviewedAllowAnonymous()
    {
        var assembly = typeof(Program).Assembly;
        var controllerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();

        Assert.NotEmpty(controllerTypes);

        var unguardedActions = new List<string>();
        var unexpectedAnonymous = new List<string>();

        foreach (var controller in controllerTypes)
        {
            var controllerHasAuthorize = controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any();
            var controllerHasAllowAnonymous = controller.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Any();

            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !m.GetCustomAttributes(typeof(NonActionAttribute), inherit: true).Any());

            foreach (var action in actions)
            {
                var key = $"{controller.Name}.{action.Name}";
                var hasHttpMethod = action.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true).Any();
                if (!hasHttpMethod)
                {
                    continue;
                }

                var actionHasAllowAnonymous = action.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Any();
                var actionHasAuthorize = action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any();

                if (controllerHasAllowAnonymous || actionHasAllowAnonymous)
                {
                    if (!ReviewedAnonymousActions.Contains(key))
                    {
                        unexpectedAnonymous.Add(key);
                    }
                    continue;
                }

                if (!controllerHasAuthorize && !actionHasAuthorize)
                {
                    unguardedActions.Add(key);
                }
            }
        }

        Assert.Empty(unguardedActions);
        Assert.Empty(unexpectedAnonymous);
    }
}
