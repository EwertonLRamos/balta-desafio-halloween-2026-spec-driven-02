using LinkShortener.Application.Links;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LinkShortener.Api.Links;

public static class LinkStatisticsEndpoints
{
    public static void MapLinkStatisticsEndpoints(this WebApplication app)
    {
        app.MapGet(
                "/api/links/{code}/stats",
                async Task<Results<Ok<LinkStatisticsResponse>, NotFound, ProblemHttpResult>> (
                    string code,
                    [FromHeader(Name = "X-Management-Key")] string? managementKey,
                    HttpContext httpContext,
                    ILinkService linkService,
                    CancellationToken cancellationToken) =>
                {
                    var result = await linkService.GetStatisticsAsync(
                        code,
                        managementKey,
                        cancellationToken);

                    return result.Status switch
                    {
                        LinkStatisticsStatus.Found =>
                            TypedResults.Ok(new LinkStatisticsResponse(
                                result.AccessCount
                                    ?? throw new InvalidOperationException(
                                        "Estatísticas encontradas devem incluir a contagem de acessos."))),
                        LinkStatisticsStatus.NotFound => TypedResults.NotFound(),
                        LinkStatisticsStatus.Unauthorized => TypedResults.Problem(
                            detail: "A chave de gerenciamento é inválida ou não foi informada.",
                            instance: httpContext.Request.Path,
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Não autorizado"),
                        _ => throw new InvalidOperationException("Resultado de estatísticas não reconhecido.")
                    };
                })
            .WithName("GetLinkStatistics")
            .WithSummary("Consulta as estatísticas de um link")
            .WithDescription("Retorna a contagem de acessos quando a chave de gerenciamento é válida.")
            .Produces<LinkStatisticsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
