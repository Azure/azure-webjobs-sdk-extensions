using Microsoft.AspNetCore.Routing;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public interface IWebJobsRouter : IRouter
    {
        IInlineConstraintResolver ConstraintResolver { get; }

        void AddFunctionRoute(IRouter route);

        void ClearRoutes();

        WebJobsRouteBuilder CreateBuilder(IWebJobsRouteHandler routeHandler, string routePrefix);
    }
}