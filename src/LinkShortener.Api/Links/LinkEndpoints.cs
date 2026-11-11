using LinkShortener.Application.Links;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LinkShortener.Api.Links;

public static class LinkEndpoints
{
    public static void MapLinkEndpoints(this WebApplication app)
    {
        app.MapPost(
                "/api/links",
                async Task<Results<
                    Created<CreateLinkResponse>,
                    ValidationProblem,
                    ProblemHttpResult>> (
                    CreateLinkRequest request,
                    HttpContext httpContext,
                    ILinkService linkService,
                    CancellationToken cancellationToken) =>
                {
                    var result = await linkService.CreateAsync(
                        request.TargetUrl,
                        request.Code,
                        cancellationToken);
                    if (!result.IsSuccessful)
                    {
                        var errorMessage = result.ErrorMessage
                            ?? throw new InvalidOperationException(
                                "Uma falha de criação deve incluir uma mensagem.");

                        if (result.IsConflict)
                        {
                            return TypedResults.Problem(
                                detail: errorMessage,
                                instance: httpContext.Request.Path,
                                statusCode: StatusCodes.Status409Conflict,
                                title: "Conflito ao criar o link");
                        }

                        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                        {
                            [result.ErrorField ?? "targetUrl"] = [errorMessage]
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
            .WithDescription(
                "Valida a URL de destino e cria um link com código gerado automaticamente ou informado pelo criador.")
            .Produces<CreateLinkResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
