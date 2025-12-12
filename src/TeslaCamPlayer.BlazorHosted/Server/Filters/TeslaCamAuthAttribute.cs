using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TeslaCamPlayer.BlazorHosted.Server.Data;

namespace TeslaCamPlayer.BlazorHosted.Server.Filters
{
    public class TeslaCamAuthAttribute : TypeFilterAttribute
    {
        public TeslaCamAuthAttribute() : base(typeof(TeslaCamAuthFilter))
        {
        }
    }

    public class TeslaCamAuthFilter : IAsyncAuthorizationFilter
    {
        private readonly TeslaCamDbContext _dbContext;

        public TeslaCamAuthFilter(TeslaCamDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = await _dbContext.Users.FindAsync("Admin");
            if (user != null && user.IsEnabled)
            {
                if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
                {
                    context.Result = new UnauthorizedResult();
                }
            }
        }
    }
}
