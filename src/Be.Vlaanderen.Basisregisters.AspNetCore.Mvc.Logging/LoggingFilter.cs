namespace Be.Vlaanderen.Basisregisters.AspNetCore.Mvc.Logging
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class LoggingFilterFactory : IFilterFactory
    {
        private static readonly string[] DefaultMethodsToLog = { "POST", "PUT" };

        private readonly string[] _methodsToLog;

        public bool IsReusable => false;

        public LoggingFilterFactory() : this(null) { }

        public LoggingFilterFactory(string[]? methodsToLog) => _methodsToLog = methodsToLog ?? DefaultMethodsToLog;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
            => new LoggingFilter((ILogger<LoggingFilter>)serviceProvider.GetRequiredService(typeof(ILogger<LoggingFilter>)), _methodsToLog);
    }

    public class LoggingFilter : IActionFilter
    {
        private readonly ILogger<LoggingFilter> _logger;
        private readonly string[] _methodsToLog;

        public LoggingFilter(ILogger<LoggingFilter> logger, string[] methodsToLog)
        {
            _logger = logger;
            _methodsToLog = methodsToLog.Select(x => x.ToLowerInvariant()).ToArray();
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            // The body is only ever logged at debug level, so with debug logging off reading it achieves nothing -
            // and it is not a free nothing: ReadToEnd below is a synchronous read of a network stream, which holds on
            // to a thread pool thread for as long as the client takes to send its body. On an API that accepts large
            // uploads, enough of those at once stop the server from draining its sockets quickly enough, and Kestrel
            // aborts the request with "Reading the request body timed out due to data arriving too slowly. See
            // MinRequestBodyDataRate." - thrown from right here.
            if (!_logger.IsEnabled(LogLevel.Debug))
                return;

            var request = context.HttpContext.Request;
            if (!_methodsToLog.Contains(request.Method.ToLowerInvariant()))
                return;

            // Without buffering the body can only be read once, and reading it here would leave nothing for the model
            // binder. Rewinding it would throw as well.
            if (!request.Body.CanSeek)
                return;

            request.Body.Position = 0;

            string httpBody;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
                httpBody = reader.ReadToEnd();

            request.Body.Position = 0;

            if (IsValidJson(httpBody, out var jsonObject))
            {
                _logger.Log(LogLevel.Debug, 0, new FormattedLogValues("Incoming HTTP {Method}: {@HttpBody}", request.Method, jsonObject), null, MessageFormatter);
            }
            else
            {
                _logger.LogDebug("Incoming HTTP {Method}: {HttpBody}", request.Method, httpBody);
            }
        }

        private static string MessageFormatter(FormattedLogValues state, Exception? error) => state.ToString();

        public void OnActionExecuted(ActionExecutedContext context) { }

        private static bool IsValidJson(string strInput, out dynamic? jsonObject)
        {
            jsonObject = null;
            strInput = strInput.Trim();

            if ((!strInput.StartsWith("{") || !strInput.EndsWith("}")) && (!strInput.StartsWith("[") || !strInput.EndsWith("]")))
                return false;

            try
            {
                jsonObject = JsonConvert.DeserializeObject<dynamic>(strInput);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
