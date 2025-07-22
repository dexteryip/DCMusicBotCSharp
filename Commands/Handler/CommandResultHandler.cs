using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Services.Commands;
using NetCord.Services;
using NetCord.Services.Commands;

namespace DCMusicBot.Commands.Handler
{
    public class MyCommandResultHandler<TContext>(MessageFlags? messageFlags = null) : ICommandResultHandler<TContext>
        where TContext : ICommandContext
    {
        public ValueTask HandleResultAsync(IExecutionResult result, TContext context, GatewayClient client, ILogger logger, IServiceProvider services)
        {

            if (result is not IFailResult failResult)
                return default;

            //var resultMessage = failResult.Message;
            var resultMessage = "?_?";

            var message = context.Message;

            if (failResult is IExceptionResult exceptionResult)
                logger.LogError(exceptionResult.Exception, "Execution of a command with content '{Content}' failed with an exception", message.Content);
            else
                logger.LogDebug("Execution of a command with content '{Content}' failed with '{Message}'", message.Content, resultMessage);

            return new(message.ReplyAsync(new()
            {
                Content = resultMessage,
                FailIfNotExists = false,
                Flags = messageFlags,
            }));
        }
    }
}
