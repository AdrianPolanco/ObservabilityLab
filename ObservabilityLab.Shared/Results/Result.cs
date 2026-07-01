namespace ObservabilityLab.Shared.Results
{
    public record Result<T> where T : class
    {
        private Result() { }
        public bool IsSuccess => !_errors.Any() && Data is not null;
        private List<Error> _errors = [];
        public IReadOnlyList<Error> Errors => _errors.AsReadOnly();
        public T? Data { get; private set; } = default(T?);

        public static Result<T> Success(T data)
        {
            return new Result<T>
            {
                Data = data
            };
        }

        public static Result<T> Failure(Error error)
        {
            return new Result<T>
            {
                _errors = new List<Error> { error }
            };
        }

        public static Result<T> Failures(List<Error> errors)
        {
            return new Result<T>
            {
                _errors = errors
            };
        }

        public void AddError(string code, string message, Dictionary<string, object>? metadata = null)
            => _errors.Add(new Error(code, message, metadata));
        public void AddErrors(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                _errors.Add(error);
            }
        }
}
}
