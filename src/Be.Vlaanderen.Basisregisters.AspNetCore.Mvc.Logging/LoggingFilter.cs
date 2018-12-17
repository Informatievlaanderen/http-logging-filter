namespace Be.Vlaanderen.Basisregisters.AspNetCore.Mvc.Logging
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Internal;
    using Newtonsoft.Json;

    public class LoggingFilterFactory : IFilterFactory
    {
        private static readonly string[] DefaultMethodsToLog = { "POST", "PUT" };

        private readonly string[] _methodsToLog;

        public bool IsReusable => false;

        public LoggingFilterFactory() : this(null) { }

        public LoggingFilterFactory(string[] methodsToLog) => _methodsToLog = methodsToLog ?? DefaultMethodsToLog;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
            => new LoggingFilter((ILogger<LoggingFilter>)serviceProvider.GetService(typeof(ILogger<LoggingFilter>)), _methodsToLog);
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
            var request = context.HttpContext.Request;
            if (!_methodsToLog.Contains(request.Method.ToLowerInvariant()))
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

        private static string MessageFormatter(object state, Exception error) => state.ToString();

        public void OnActionExecuted(ActionExecutedContext context) { }

        private static bool IsValidJson(string strInput, out dynamic jsonObject)
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
