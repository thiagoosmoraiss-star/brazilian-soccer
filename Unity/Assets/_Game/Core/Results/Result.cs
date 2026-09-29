using System;
using System.Collections.Generic;

namespace Game.Core.Results
{
    /// <summary>Error of an operation: stable machine-readable code + human-readable message.</summary>
    public readonly struct Error
    {
        public readonly string Code;
        public readonly string Message;

        public Error(string code, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? string.Empty;
        }

        public override string ToString() => Code + ": " + Message;
    }

    /// <summary>Outcome of an operation without a value.</summary>
    public sealed class Result
    {
        private static readonly IReadOnlyList<Error> NoErrors = Array.Empty<Error>();
        public static readonly Result Success = new Result(NoErrors);

        public IReadOnlyList<Error> Errors { get; }
        public bool IsSuccess => Errors.Count == 0;

        private Result(IReadOnlyList<Error> errors) { Errors = errors; }

        public static Result Fail(string code, string message) => new Result(new[] { new Error(code, message) });

        public static Result Fail(IReadOnlyList<Error> errors)
        {
            if (errors == null || errors.Count == 0) throw new ArgumentException("A failure needs at least one error.", nameof(errors));
            return new Result(errors);
        }

        public static Result From(IReadOnlyList<Error> errors) =>
            errors == null || errors.Count == 0 ? Success : new Result(errors);

        public override string ToString() => IsSuccess ? "Success" : string.Join("; ", Errors);
    }

    /// <summary>Outcome of an operation that produces a value on success.</summary>
    public sealed class Result<T>
    {
        public IReadOnlyList<Error> Errors { get; }
        public bool IsSuccess => Errors.Count == 0;

        private readonly T _value;

        public T Value
        {
            get
            {
                if (!IsSuccess) throw new InvalidOperationException("Result has no value: " + string.Join("; ", Errors));
                return _value;
            }
        }

        private Result(T value, IReadOnlyList<Error> errors) { _value = value; Errors = errors; }

        public static Result<T> Ok(T value) => new Result<T>(value, Array.Empty<Error>());

        public static Result<T> Fail(string code, string message) => new Result<T>(default, new[] { new Error(code, message) });

        public static Result<T> Fail(IReadOnlyList<Error> errors)
        {
            if (errors == null || errors.Count == 0) throw new ArgumentException("A failure needs at least one error.", nameof(errors));
            return new Result<T>(default, errors);
        }

        public override string ToString() => IsSuccess ? "Ok(" + _value + ")" : string.Join("; ", Errors);
    }
}
