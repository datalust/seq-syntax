// Copyright © Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Seq.Syntax.Expressions.Ast;
using Seq.Syntax.Expressions.Compilation.Transformations;
using Seq.Syntax.Expressions.Parsing;
using Seq.Syntax.Expressions.Runtime;
using Seq.Syntax.Templates.Compilation;
using Seq.Syntax.Templates.Encoding;
using Expression = Seq.Syntax.Expressions.Ast.Expression;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;
using LX = System.Linq.Expressions.Expression;
using ExpressionBody = System.Linq.Expressions.Expression;

namespace Seq.Syntax.Expressions.Compilation.Linq;

class LinqExpressionCompiler : SeqExpressionTransformer<ExpressionBody>
{
    readonly NameResolver _nameResolver;
    readonly CultureInfo? _formatProvider;

    static readonly MethodInfo CollectSequenceElementsMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.CollectSequenceElements), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo ExtendSequenceValueWithSpreadMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.ExtendSequenceValueWithSpread), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo ExtendSequenceValueWithItemMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.ExtendSequenceValueWithItem), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo ConstructSequenceValueMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.ConstructSequenceValue), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo ConstructStructureValueMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.ConstructStructureValue), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo CollectStructurePropertiesMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.CollectStructureProperties), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo CompleteStructureValueMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.CompleteStructureValue), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo ExtendStructureValueWithSpreadMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.ExtendStructureValueWithSpread), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo ExtendStructureValueWithPropertyMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.ExtendStructureValueWithProperty), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo CoerceToScalarBooleanMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.CoerceToScalarBoolean), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo ScalarBooleanMethod = typeof(RuntimeOperators)
        .GetMethod(nameof(RuntimeOperators.ScalarBoolean), BindingFlags.Static | BindingFlags.NonPublic)!;

    static readonly MethodInfo IndexOfMatchMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.IndexOfMatch), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo TryGetStructurePropertyValueMethod = typeof(Intrinsics)
        .GetMethod(nameof(Intrinsics.TryGetStructurePropertyValue), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo MakeCallable1Method = typeof(Values)
        .GetMethod(nameof(Values.MakeCallable), [typeof(Func<EvaluationResult, EvaluationResult>)])!;

    static readonly MethodInfo MakeCallable2Method = typeof(Values)
        .GetMethod(nameof(Values.MakeCallable), [typeof(Func<EvaluationResult, EvaluationResult, EvaluationResult>)])!;

    static readonly PropertyInfo EvaluationContextDocumentProperty = typeof(EvaluationContext)
        .GetProperty(nameof(EvaluationContext.Document), BindingFlags.Instance | BindingFlags.Public)!;

    static readonly PropertyInfo EvaluationResultIsDefinedProperty = typeof(EvaluationResult)
        .GetProperty(nameof(EvaluationResult.IsDefined), BindingFlags.Instance | BindingFlags.Public)!;

    static readonly PropertyInfo EvaluationResultDefinedValueProperty = typeof(EvaluationResult)
        .GetProperty("ReflectionOnlyDefinedValue", BindingFlags.Instance | BindingFlags.NonPublic)!;

    static readonly MethodInfo NumericOrNullMethod = typeof(Coerce)
        .GetMethod(nameof(Coerce.NumericOrDefault), BindingFlags.Static | BindingFlags.NonPublic)!;

    static readonly PropertyInfo NullableDecimalHasValueProperty = typeof(decimal?)
        .GetProperty(nameof(Nullable<>.HasValue))!;

    static readonly PropertyInfo NullableDecimalValueProperty = typeof(decimal?)
        .GetProperty(nameof(Nullable<>.Value))!;

    static readonly MethodInfo StringOrNullMethod = typeof(Coerce)
        .GetMethod(nameof(Coerce.StringOrDefault), BindingFlags.Static | BindingFlags.NonPublic)!;

    static readonly MethodInfo BooleanOrNullMethod = typeof(Coerce)
        .GetMethod(nameof(Coerce.BooleanOrDefault), BindingFlags.Static | BindingFlags.NonPublic)!;

    static readonly PropertyInfo NullableBooleanHasValueProperty = typeof(bool?)
        .GetProperty(nameof(Nullable<>.HasValue))!;

    static readonly PropertyInfo NullableBooleanValueProperty = typeof(bool?)
        .GetProperty(nameof(Nullable<>.Value))!;

    static readonly MethodInfo DateTimeOffsetOrNullMethod = typeof(Coerce)
        .GetMethod(nameof(Coerce.DateTimeOffsetOrDefault), BindingFlags.Static | BindingFlags.NonPublic)!;

    static readonly PropertyInfo NullableDateTimeOffsetHasValueProperty = typeof(DateTimeOffset?)
        .GetProperty(nameof(Nullable<>.HasValue))!;

    static readonly PropertyInfo NullableDateTimeOffsetValueProperty = typeof(DateTimeOffset?)
        .GetProperty(nameof(Nullable<>.Value))!;

    static readonly MethodInfo TimeSpanOrNullMethod = typeof(Coerce)
        .GetMethod(nameof(Coerce.TimeSpanOrDefault), BindingFlags.Static | BindingFlags.NonPublic)!;

    static readonly PropertyInfo NullableTimeSpanHasValueProperty = typeof(TimeSpan?)
        .GetProperty(nameof(Nullable<>.HasValue))!;

    static readonly PropertyInfo NullableTimeSpanValueProperty = typeof(TimeSpan?)
        .GetProperty(nameof(Nullable<>.Value))!;

    static readonly System.Linq.Expressions.ConstantExpression UndefinedConstant =
        LX.Constant(EvaluationResult.Undefined, typeof(EvaluationResult));

    readonly NullabilityInfoContext _nullabilityContext = new();

    ParameterExpression Context { get; } = LX.Variable(typeof(EvaluationContext), "ctx");

    LinqExpressionCompiler(CultureInfo? formatProvider, NameResolver nameResolver)
    {
        _nameResolver = nameResolver;
        _formatProvider = formatProvider;
    }

    public static Evaluatable Compile(Expression expression, CultureInfo? formatProvider,
        NameResolver nameResolver)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var compiler = new LinqExpressionCompiler(formatProvider, nameResolver);
        var body = compiler.Transform(expression);
        return LX.Lambda<Evaluatable>(body, compiler.Context).Compile();
    }

    ExpressionBody Splice(Expression<Evaluatable> lambda)
    {
        return ParameterReplacementVisitor.ReplaceParameters(lambda, Context);
    }

    protected override ExpressionBody Transform(CallExpression call)
    {
        if (!_nameResolver.TryResolveFunctionName(call.OperatorName, out var m))
            throw new ArgumentException($"The function name `{call.OperatorName}` was not recognized.");

        if (m == null!)
            throw new InvalidOperationException(
                $"The name resolver {_nameResolver} failed to return a valid `MethodInfo` for function `{call.OperatorName}`.");

        if (m.ReturnType != typeof(EvaluationResult))
            throw new ArgumentException(
                $"The method `{m.Name}` implementing function `{call.OperatorName}` returns `{m.ReturnType}`, but methods implementing functions must return `{nameof(EvaluationResult)}`.");

        var methodParameters = m.GetParameters()
            .Select(info => (pi: info, optional: info.GetCustomAttribute<OptionalAttribute>() != null))
            .ToList();

        var allowedParameters = methodParameters.Where(info => IsOperandParameter(info.pi)).ToList();
        var requiredParameterCount = allowedParameters.Count(info => !info.optional);

        if (call.Operands.Length < requiredParameterCount || call.Operands.Length > allowedParameters.Count)
        {
            var requirements = DescribeRequirements(allowedParameters.Select(info => (info.pi.Name!, info.optional)).ToList());
            throw new ArgumentException($"The function `{call.OperatorName}` {requirements}.");
        }

        var operands = new Queue<LX>(call.Operands.Select(Transform));

        // `and` and `or` short-circuit to save execution time; unlike the earlier Serilog.Filters.Expressions, nothing else does.
        if (Operators.SameOperator(call.OperatorName, Operators.RuntimeOpAnd))
            return CompileLogical(LX.AndAlso, operands.Dequeue(), operands.Dequeue());

        if (Operators.SameOperator(call.OperatorName, Operators.RuntimeOpOr))
            return CompileLogical(LX.OrElse, operands.Dequeue(), operands.Dequeue());

        // A `JsonNode?`, `decimal`/`decimal?`, `bool`/`bool?`, `DateTimeOffset`/`DateTimeOffset?`,
        // `TimeSpan`/`TimeSpan?`, or `string`/`string?` parameter receives a *coerced* argument,
        // and the call is short-circuited to `undefined` when the argument doesn't satisfy it: a
        // `JsonNode?` wants any defined value; a non-nullable parameter a defined operand of its
        // kind; and a nullable one an undefined-or-coercible operand (so a *defined* operand that
        // is JSON null or the wrong kind short-circuits, while an undefined one passes through as
        // `null`). When there are such parameters we evaluate every operand into a temp (in source
        // order) first, so all operands are still evaluated — preserving evaluation order and any
        // suppressed-error diagnostics — and only the operator call, and the coercions feeding it,
        // are skipped.
        var hasCoercedParams = methodParameters.Any(info => IsCoercedParameter(info.pi));

        var boundParameters = new List<LX>(methodParameters.Count);
        var operandTemps = new List<(ParameterExpression temp, LX operand)>();
        var coercionTemps = new List<ParameterExpression>();
        var guards = new List<ExpressionBody>();

        foreach (var (pi, optional) in methodParameters)
        {
            if (pi.ParameterType == typeof(EvaluationResult))
            {
                if (operands.Count == 0)
                    boundParameters.Add(UndefinedConstant);
                else if (!hasCoercedParams)
                    boundParameters.Add(operands.Dequeue());
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    boundParameters.Add(temp);
                }
            }
            else if (pi.ParameterType == typeof(JsonNode))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(null, typeof(JsonNode)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    guards.Add(LX.Property(temp, EvaluationResultIsDefinedProperty));
                    boundParameters.Add(LX.Property(temp, EvaluationResultDefinedValueProperty));
                }
            }
            else if (pi.ParameterType == typeof(decimal))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(
                        pi.GetCustomAttribute<DefaultParameterValueAttribute>()?.Value ?? 0, typeof(decimal)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var numeric = LX.Variable(typeof(decimal?), pi.Name + "_numeric");
                    coercionTemps.Add(numeric);
                    guards.Add(LX.Property(
                        LX.Assign(numeric, LX.Call(NumericOrNullMethod, temp)),
                        NullableDecimalHasValueProperty));
                    boundParameters.Add(LX.Property(numeric, NullableDecimalValueProperty));
                }
            }
            else if (pi.ParameterType == typeof(decimal?))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(null, typeof(decimal?)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var numeric = LX.Variable(typeof(decimal?), pi.Name + "_numeric");
                    coercionTemps.Add(numeric);
                    guards.Add(LX.OrElse(
                        LX.Not(LX.Property(temp, EvaluationResultIsDefinedProperty)),
                        LX.Property(
                            LX.Assign(numeric, LX.Call(NumericOrNullMethod, temp)),
                            NullableDecimalHasValueProperty)));
                    boundParameters.Add(numeric);
                }
            }
            else if (pi.ParameterType == typeof(bool))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(
                        pi.GetCustomAttribute<DefaultParameterValueAttribute>()?.Value ?? false, typeof(bool)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var boolean = LX.Variable(typeof(bool?), pi.Name + "_boolean");
                    coercionTemps.Add(boolean);
                    guards.Add(LX.Property(
                        LX.Assign(boolean, LX.Call(BooleanOrNullMethod, temp)),
                        NullableBooleanHasValueProperty));
                    boundParameters.Add(LX.Property(boolean, NullableBooleanValueProperty));
                }
            }
            else if (pi.ParameterType == typeof(bool?))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(null, typeof(bool?)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var boolean = LX.Variable(typeof(bool?), pi.Name + "_boolean");
                    coercionTemps.Add(boolean);
                    guards.Add(LX.OrElse(
                        LX.Not(LX.Property(temp, EvaluationResultIsDefinedProperty)),
                        LX.Property(
                            LX.Assign(boolean, LX.Call(BooleanOrNullMethod, temp)),
                            NullableBooleanHasValueProperty)));
                    boundParameters.Add(boolean);
                }
            }
            else if (pi.ParameterType == typeof(DateTimeOffset))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(default(DateTimeOffset), typeof(DateTimeOffset)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var dateTime = LX.Variable(typeof(DateTimeOffset?), pi.Name + "_dateTime");
                    coercionTemps.Add(dateTime);
                    guards.Add(LX.Property(
                        LX.Assign(dateTime, LX.Call(DateTimeOffsetOrNullMethod, temp)),
                        NullableDateTimeOffsetHasValueProperty));
                    boundParameters.Add(LX.Property(dateTime, NullableDateTimeOffsetValueProperty));
                }
            }
            else if (pi.ParameterType == typeof(DateTimeOffset?))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(null, typeof(DateTimeOffset?)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var dateTime = LX.Variable(typeof(DateTimeOffset?), pi.Name + "_dateTime");
                    coercionTemps.Add(dateTime);
                    guards.Add(LX.OrElse(
                        LX.Not(LX.Property(temp, EvaluationResultIsDefinedProperty)),
                        LX.Property(
                            LX.Assign(dateTime, LX.Call(DateTimeOffsetOrNullMethod, temp)),
                            NullableDateTimeOffsetHasValueProperty)));
                    boundParameters.Add(dateTime);
                }
            }
            else if (pi.ParameterType == typeof(TimeSpan))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(default(TimeSpan), typeof(TimeSpan)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var timeSpan = LX.Variable(typeof(TimeSpan?), pi.Name + "_timeSpan");
                    coercionTemps.Add(timeSpan);
                    guards.Add(LX.Property(
                        LX.Assign(timeSpan, LX.Call(TimeSpanOrNullMethod, temp)),
                        NullableTimeSpanHasValueProperty));
                    boundParameters.Add(LX.Property(timeSpan, NullableTimeSpanValueProperty));
                }
            }
            else if (pi.ParameterType == typeof(TimeSpan?))
            {
                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(null, typeof(TimeSpan?)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var timeSpan = LX.Variable(typeof(TimeSpan?), pi.Name + "_timeSpan");
                    coercionTemps.Add(timeSpan);
                    guards.Add(LX.OrElse(
                        LX.Not(LX.Property(temp, EvaluationResultIsDefinedProperty)),
                        LX.Property(
                            LX.Assign(timeSpan, LX.Call(TimeSpanOrNullMethod, temp)),
                            NullableTimeSpanHasValueProperty)));
                    boundParameters.Add(timeSpan);
                }
            }
            else if (IsStringOperand(pi))
            {
                var nullable = _nullabilityContext.Create(pi).WriteState == NullabilityState.Nullable;

                if (operands.Count == 0)
                    boundParameters.Add(LX.Constant(
                        nullable ? null : pi.GetCustomAttribute<DefaultParameterValueAttribute>()?.Value, typeof(string)));
                else
                {
                    var temp = LX.Variable(typeof(EvaluationResult), pi.Name);
                    operandTemps.Add((temp, operands.Dequeue()));
                    var str = LX.Variable(typeof(string), pi.Name + "_string");
                    coercionTemps.Add(str);
                    var coerced = LX.ReferenceNotEqual(
                        LX.Assign(str, LX.Call(StringOrNullMethod, temp)),
                        LX.Constant(null, typeof(string)));
                    guards.Add(nullable
                        ? LX.OrElse(LX.Not(LX.Property(temp, EvaluationResultIsDefinedProperty)), coerced)
                        : coerced);
                    boundParameters.Add(str);
                }
            }
            else if (pi.ParameterType == typeof(StringComparison))
                boundParameters.Add(LX.Constant(call.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            else if (pi.ParameterType == typeof(CultureInfo))
                boundParameters.Add(LX.Constant(_formatProvider, typeof(CultureInfo)));
            else if (pi.ParameterType == typeof(IFormatProvider))
                boundParameters.Add(LX.Constant(_formatProvider, typeof(IFormatProvider)));
            else if (pi.ParameterType == typeof(JsonObject))
                boundParameters.Add(LX.Property(Context, EvaluationContextDocumentProperty));
            else if (_nameResolver.TryBindFunctionParameter(pi, out var binding))
                boundParameters.Add(LX.Constant(binding, pi.ParameterType));
            else if (optional)
                boundParameters.Add(LX.Constant(
                    pi.GetCustomAttribute<DefaultParameterValueAttribute>()?.Value, pi.ParameterType));
            else
                throw new ArgumentException($"The method `{m.Name}` implementing function `{call.OperatorName}` has argument `{pi.Name}` which could not be bound.");
        }

        var invocation = LX.Call(m, boundParameters);
        if (!hasCoercedParams)
            return invocation;

        ExpressionBody guarded = guards.Count > 0
            ? LX.Condition(
                guards.Aggregate(LX.AndAlso),
                invocation,
                UndefinedConstant,
                typeof(EvaluationResult))
            : invocation;

        var block = new List<ExpressionBody>(operandTemps.Count + 1);
        foreach (var (temp, operand) in operandTemps)
            block.Add(LX.Assign(temp, operand));
        block.Add(guarded);

        return LX.Block(operandTemps.Select(t => t.temp).Concat(coercionTemps), block);
    }

    bool IsOperandParameter(ParameterInfo pi) =>
        pi.ParameterType == typeof(EvaluationResult) || IsCoercedParameter(pi);

    bool IsCoercedParameter(ParameterInfo pi) =>
        pi.ParameterType == typeof(JsonNode) ||
        pi.ParameterType == typeof(decimal) ||
        pi.ParameterType == typeof(decimal?) ||
        pi.ParameterType == typeof(bool) ||
        pi.ParameterType == typeof(bool?) ||
        pi.ParameterType == typeof(DateTimeOffset) ||
        pi.ParameterType == typeof(DateTimeOffset?) ||
        pi.ParameterType == typeof(TimeSpan) ||
        pi.ParameterType == typeof(TimeSpan?) ||
        IsStringOperand(pi);

    bool IsStringOperand(ParameterInfo pi) =>
        pi.ParameterType == typeof(string) && !_nameResolver.TryBindFunctionParameter(pi, out _);

    static string DescribeRequirements(IReadOnlyList<(string name, bool optional)> parameters)
    {
        static string DescribeArgument((string name, bool optional) p) =>
            $"`{p.name}`" + (p.optional ? " (optional)" : "");

        if (parameters.Count == 0)
            return "accepts no arguments";

        if (parameters.Count == 1)
            return $"accepts one argument, {DescribeArgument(parameters[0])}";

        if (parameters.Count == 2)
            return $"accepts two arguments, {DescribeArgument(parameters[0])} and {DescribeArgument(parameters[1])}";

        var result = new StringBuilder("accepts arguments");
        for (var i = 0; i < parameters.Count - 1; ++i)
            result.Append($" {DescribeArgument(parameters[i])},");

        result.Append($" and {DescribeArgument(parameters[^1])}");
        return result.ToString();
    }

    static ExpressionBody CompileLogical(Func<ExpressionBody, ExpressionBody, ExpressionBody> apply, ExpressionBody lhs, ExpressionBody rhs)
    {
        return LX.Call(
            ScalarBooleanMethod,
            apply(
                LX.Call(CoerceToScalarBooleanMethod, lhs),
                LX.Call(CoerceToScalarBooleanMethod, rhs)));
    }

    protected override ExpressionBody Transform(AccessorExpression spx)
    {
        var receiver = Transform(spx.Receiver);
        return LX.Call(TryGetStructurePropertyValueMethod, LX.Constant(StringComparison.Ordinal), receiver, LX.Constant(spx.MemberName, typeof(string)));
    }

    protected override ExpressionBody Transform(Ast.ConstantExpression cx)
    {
        return LX.Constant(cx.Constant, typeof(EvaluationResult));
    }

    protected override ExpressionBody Transform(AmbientNameExpression px)
    {
        if (px.IsBuiltIn)
        {
            switch (px.PropertyName)
            {
                case KeywordProperties.Timestamp:
                    return Splice(context => KeywordProperties.GetTimestamp(context.Document));
                case KeywordProperties.Level:
                    return Splice(context => KeywordProperties.GetLevel(context.Document));
                case KeywordProperties.Message:
                {
                    var formatter = new CompiledMessageToken(_formatProvider, null, TemplateOutputEncoder.Default);
                    return Splice(context => KeywordProperties.GetMessage(formatter, context));
                }
                case KeywordProperties.MessageTemplate:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@mt"));
                case KeywordProperties.Exception:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@x"));
                case KeywordProperties.EventType:
                    return Splice(context => KeywordProperties.GetEventType(context.Document));
                case KeywordProperties.Properties:
                    return Splice(context => KeywordProperties.GetProperties(context.Document));
                case KeywordProperties.Id:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@seqid"));
                case KeywordProperties.TraceId:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@tr"));
                case KeywordProperties.SpanId:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@sp"));
                case KeywordProperties.ParentId:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@ps"));
                case KeywordProperties.SpanKind:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@sk"));
                case KeywordProperties.Start:
                    return Splice(context => KeywordProperties.GetStart(context.Document));
                case KeywordProperties.Elapsed:
                    return Splice(context => KeywordProperties.GetElapsed(context.Document));
                case KeywordProperties.Resource:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@ra"));
                case KeywordProperties.Scope:
                    return Splice(context => Intrinsics.GetPropertyValue(context, "@sa"));
                case KeywordProperties.Data:
                    return Splice(context => KeywordProperties.GetData(context.Document));
                case KeywordProperties.Arrived:
                case KeywordProperties.Document:
                    return UndefinedConstant;
            }

            if (_nameResolver.TryResolveBuiltInPropertyName(px.PropertyName, out var target))
                return Transform(ExpressionCompiler.Translate(new ExpressionParser().Parse(target)));

            var atName = "@" + px.PropertyName;
            return Splice(context => Intrinsics.GetPropertyValue(context, atName));
        }

        // Don't close over the AST node.
        var propertyName = px.PropertyName;
        return Splice(context => Intrinsics.GetPropertyValue(context, propertyName));
    }

    protected override ExpressionBody Transform(LocalNameExpression nlx)
    {
        // Don't close over the AST node.
        var name = nlx.Name;
        return Splice(context => Intrinsics.GetLocalValue(context, name));
    }

    protected override ExpressionBody Transform(Ast.LambdaExpression lmx)
    {
        var parameters = lmx.Parameters.Select(px => Tuple.Create(px, LX.Parameter(typeof(EvaluationResult), px.ParameterName))).ToList();
        var paramSwitcher = new ExpressionConstantMapper(parameters.ToDictionary(px => (object)px.Item1, px => (System.Linq.Expressions.Expression)px.Item2));
        var rewritten = paramSwitcher.Visit(Transform(lmx.Body));

        MethodInfo makeCallable;
        Type delegateType;
        if (lmx.Parameters.Length == 1)
        {
            delegateType = typeof(Func<EvaluationResult, EvaluationResult>);
            makeCallable = MakeCallable1Method;
        }
        else if (lmx.Parameters.Length == 2)
        {
            delegateType = typeof(Func<EvaluationResult, EvaluationResult, EvaluationResult>);
            makeCallable = MakeCallable2Method;
        }
        else
            throw new NotSupportedException("Unsupported lambda signature.");

        var lambda = LX.Lambda(delegateType, rewritten, parameters.Select(px => px.Item2).ToArray());

        // Functions are threaded through as CLR-backed `JsonValue`s.
        return LX.Call(makeCallable, lambda);
    }

    protected override ExpressionBody Transform(Ast.ParameterExpression prx)
    {
        // Will be within a lambda, which will subsequently sub-in the actual value.
        // The `prx` placeholder is wrapped as a CLR-backed value so that eager
        // typechecking doesn't fail before we've substituted the real value in.
        return LX.Constant(EvaluationResult.Defined(JsonValue.Create(prx)), typeof(EvaluationResult));
    }

    protected override ExpressionBody Transform(IndexerWildcardExpression wx)
    {
        return UndefinedConstant;
    }

    protected override ExpressionBody Transform(ArrayExpression ax)
    {
        var elements = new List<ExpressionBody>(ax.Elements.Length);
        var i = 0;
        for (; i < ax.Elements.Length; ++i)
        {
            var element = ax.Elements[i];
            if (element is ItemElement item)
                elements.Add(Transform(item.Value));
            else
                break;
        }

        var arr = LX.NewArrayInit(typeof(EvaluationResult), elements.ToArray());
        var collected = LX.Call(CollectSequenceElementsMethod, arr);

        for (; i < ax.Elements.Length; ++i)
        {
            var element = ax.Elements[i];
            if (element is ItemElement item)
                collected = LX.Call(ExtendSequenceValueWithItemMethod, collected, Transform(item.Value));
            else
            {
                var spread = (SpreadElement)element;
                collected = LX.Call(ExtendSequenceValueWithSpreadMethod, collected, Transform(spread.Content));
            }
        }

        return LX.Call(ConstructSequenceValueMethod, collected);
    }

    protected override ExpressionBody Transform(ObjectExpression ox)
    {
        var names = new List<string>();
        var values = new List<ExpressionBody>();

        var i = 0;
        for (; i < ox.Members.Length; ++i)
        {
            var member = ox.Members[i];
            if (member is PropertyMember property)
            {
                if (names.Contains(property.Name))
                {
                    var oldPos = names.IndexOf(property.Name);
                    values[oldPos] = Transform(property.Value);
                }
                else
                {
                    names.Add(property.Name);
                    values.Add(Transform(property.Value));
                }
            }
            else
            {
                break;
            }
        }

        var namesConstant = LX.Constant(names.ToArray(), typeof(string[]));
        var valuesArr = LX.NewArrayInit(typeof(EvaluationResult), values.ToArray());

        if (i == ox.Members.Length)
        {
            // No spreads; last-in-wins member erasure is not required.
            return LX.Call(ConstructStructureValueMethod, namesConstant, valuesArr);
        }

        var properties = LX.Call(CollectStructurePropertiesMethod, namesConstant, valuesArr);

        for (; i < ox.Members.Length; ++i)
        {
            var member = ox.Members[i];
            if (member is PropertyMember property)
            {
                properties = LX.Call(
                    ExtendStructureValueWithPropertyMethod,
                    properties,
                    LX.Constant(property.Name),
                    Transform(property.Value));
            }
            else
            {
                var spread = (SpreadMember)member;
                properties = LX.Call(
                    ExtendStructureValueWithSpreadMethod,
                    properties,
                    Transform(spread.Content));
            }
        }

        return LX.Call(CompleteStructureValueMethod, properties);
    }

    protected override ExpressionBody Transform(IndexerExpression ix)
    {
        return Transform(new CallExpression(false, Operators.OpElementAt, ix.Receiver, ix.Index));
    }

    protected override ExpressionBody Transform(IndexOfMatchExpression mx)
    {
        var rx = LX.Constant(mx.Regex);
        var target = Transform(mx.Corpus);
        return LX.Call(IndexOfMatchMethod, target, rx);
    }
}
