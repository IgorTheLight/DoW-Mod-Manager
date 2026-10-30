namespace DoW_Mod_Manager.Services
{
    /// <summary>
    /// Cross-platform port of the <c>warnings.log</c> scanning in the legacy
    /// <c>CheckForErrorsButton_Click</c>.
    /// </summary>
    public sealed class WarningsLogService : IWarningsLogService
    {
        private const string WarningsLogFileName = "warnings.log";
        private const string FatalDataError = "Fatal Data Error";

        /// <inheritdoc />
        public IReadOnlyList<string> GetFatalDataErrors(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("Directory must be provided.", nameof(directory));

            string logPath = GamePaths.Resolve(directory, WarningsLogFileName);

            var errors = new List<string>();

            if (!File.Exists(logPath))
                return errors;

            foreach (string line in File.ReadLines(logPath))
            {
                if (line.Contains(FatalDataError))
                    errors.Add(line);
            }

            return errors;
        }
    }
}
