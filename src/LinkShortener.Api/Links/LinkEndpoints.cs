using LinkShortener.Application.Links;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LinkShortener.Api.Links;

public static class LinkEndpoints
{
    public static void MapLinkEndpoints(this WebApplication app)
    {
        app.MapPost(
                "/api/links",
                async Task<Results<Created<CreateLinkResponse>, ValidationProblem>> (
                    CreateLinkRequest request,
                    HttpContext httpContext,
                    ILinkService linkService,
                    CancellationToken cancellationToken) =>
                {
                    var result = await linkService.CreateAsync(request.TargetUrl, cancellationToken);
                    if (!result.IsSuccessful)
                    {
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["targetUrl"] =
                            [
                                result.ErrorMessage
                                    ?? throw new InvalidOperationException(
                                        "Uma falha de criação deve incluir uma mensagem.")
                            ]
                        });
                    }

                    var code = result.Code
                        ?? throw new InvalidOperationException("Um link criado deve incluir um código.");
                    var response = new CreateLinkResponse(
                        $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{code}",
                        code,
                        result.ManagementKey
                            ?? throw new InvalidOperationException(
                                "Um link criado deve incluir uma chave de gerenciamento."));

                    return TypedResults.Created($"/{code}", response);
                })
            .WithName("CreateLink")
            .WithSummary("Cria um link curto")
            .WithDescription("Valida a URL de destino e cria um link com código gerado automaticamente.")
            .Produces<CreateLinkResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
    }
}
