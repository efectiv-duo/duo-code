using duo_code.Tools.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace duo_code.Tools;

public class ControllerEndpointAction : ToolActionBase
{
    public override string ToolName => "CONTROLLER_ENDPOINT";
    public override string Description => @"Creates or updates a controller endpoint in the backend project.
Format:
CONTROLLER_ENDPOINT: ControllerName
[Full controller code]

The controller code should:
- Include necessary using statements and feature imports
- Use namespace Api.Endpoints
- Inherit from ApiControllerBase
- Include HTTP methods (Get, Post, Put, Delete)
- Use MediatR to send commands/queries

ApiControllerBase.cs file:
using Api.Filters;

using MediatR;

using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

[ApiController]
[ApiExceptionFilter]
[Route(""api/[controller]"")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetService<ISender>()!;
}

Example:
CONTROLLER_ENDPOINT: ProductController
using Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

using CreateProduct = Application.Features.Products.CreateProduct;
using GetProductsWithPagination = Application.Features.Products.GetProductsWithPagination;
using UpdateProduct = Application.Features.Products.UpdateProduct;
using DeleteProduct = Application.Features.Products.DeleteProduct;

namespace Api.Endpoints;

[ApiController]
public class ProductController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateProduct.CreateProductCommand command)
    {
        return await Mediator.Send(command);
    }

    [HttpGet]
    public Task<PaginatedList<GetProductsWithPagination.ProductBriefResponse>> GetProducts(
        [FromQuery] GetProductsWithPagination.GetProductsWithPaginationQuery query)
    {
        return Mediator.Send(query);
    }

    [HttpPut(""{id}"")]
    public async Task<ActionResult> Update(int id, UpdateProduct.UpdateProductCommand command)
    {
        if (id != command.Id)
            return BadRequest();

        await Mediator.Send(command);
        return NoContent();
    }

    [HttpDelete(""{id}"")]
    public async Task<ActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteProduct.DeleteProductCommand(id));
        return NoContent();
    }
}";

    public override bool RequiresConfirmation => true;

    public string ControllerName { get; set; } = string.Empty;
    public string ControllerCode { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(ControllerName))
            return "Error: Controller name is required";

        if (!ControllerName.EndsWith("Controller"))
            return $"Error: Controller name '{ControllerName}' must end with 'Controller'";

        // Find backend project
        var backendPath = FindBackendPath(baseDirectory);
        if (string.IsNullOrEmpty(backendPath))
            return "Error: Could not find backend project";

        var output = new StringBuilder();

        // Check if this is a delete operation (empty content)
        if (string.IsNullOrWhiteSpace(ControllerCode))
        {
            // Delete controller file
            var controllerToDelete = Path.Combine(backendPath, "src", "Api", "Endpoints", $"{ControllerName}.cs");
            if (File.Exists(controllerToDelete))
            {
                File.Delete(controllerToDelete);
                output.AppendLine($"✓ Deleted controller: {ControllerName}");
                ConsoleResultMessage = $"Controller '{ControllerName}' deleted";
            }
            else
            {
                output.AppendLine($"Controller file not found: {ControllerName}");
                ConsoleResultMessage = $"Controller '{ControllerName}' not found";
            }
            return output.ToString();
        }

        // Create or update controller file
        var endpointsPath = Path.Combine(backendPath, "src", "Api", "Endpoints");
        Directory.CreateDirectory(endpointsPath);

        var controllerFilePath = Path.Combine(endpointsPath, $"{ControllerName}.cs");
        var isUpdate = File.Exists(controllerFilePath);

        File.WriteAllText(controllerFilePath, ControllerCode);
        output.AppendLine($"✓ {(isUpdate ? "Updated" : "Created")} controller: {ControllerName}");

        ConsoleResultMessage = $"Controller '{ControllerName}' processed";
        return output.ToString();
    }

    private string FindBackendPath(string baseDirectory)
    {
        var searchPaths = new[]
        {
            Path.Combine(baseDirectory, "projects", "backend"),
            Path.Combine(baseDirectory, "bin", "Debug", "net8.0", "projects", "backend"),
            Path.Combine(baseDirectory, "backend"),
            baseDirectory
        };

        return searchPaths.FirstOrDefault(path =>
            Directory.Exists(Path.Combine(path, "src", "Api"))) ?? string.Empty;
    }

    public override string ToString() => $"{ToolName}: {ControllerName}";
}