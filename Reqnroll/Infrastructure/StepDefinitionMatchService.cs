using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Reqnroll.Bindings;
using Reqnroll.Bindings.Reflection;
using Reqnroll.ErrorHandling;
using Reqnroll.Assist;

namespace Reqnroll.Infrastructure
{
    public enum StepDefinitionAmbiguityReason
    {
        None,
        AmbiguousSteps,
        AmbiguousScopes,
        ParameterErrors
    }

    public interface IStepDefinitionMatchService
    {
        bool Ready { get; }
        BindingMatch GetBestMatch(StepInstance stepInstance, CultureInfo bindingCulture, out StepDefinitionAmbiguityReason ambiguityReason, out List<BindingMatch> candidatingMatches);
        BindingMatch Match(IStepDefinitionBinding stepDefinitionBinding, StepInstance stepInstance, CultureInfo bindingCulture, bool useRegexMatching = true, bool useParamMatching = true, bool useScopeMatching = true);
    }

    public class StepDefinitionMatchService : IStepDefinitionMatchService
    {
        private readonly IBindingRegistry _bindingRegistry;
        private readonly IStepArgumentTypeConverter _stepArgumentTypeConverter;
        private readonly IErrorProvider _errorProvider;
        private readonly IMatchArgumentCalculator _matchArgumentCalculator;

        public StepDefinitionMatchService(IBindingRegistry bindingRegistry, IStepArgumentTypeConverter stepArgumentTypeConverter, IErrorProvider errorProvider, IMatchArgumentCalculator matchArgumentCalculator )
        {
            _bindingRegistry = bindingRegistry;
            _stepArgumentTypeConverter = stepArgumentTypeConverter;
            _errorProvider = errorProvider;
            _matchArgumentCalculator = matchArgumentCalculator;
        }

        private bool CanConvertArg(object value, IBindingType typeToConvertTo, CultureInfo bindingCulture)
        {
            if (value.GetType().IsAssignableTo(typeToConvertTo))
                return true;

            return _stepArgumentTypeConverter.CanConvert(value, typeToConvertTo, bindingCulture);
        }

        public bool Ready
        {
            get { return _bindingRegistry.Ready; }
        }

        public BindingMatch Match(IStepDefinitionBinding stepDefinitionBinding, StepInstance stepInstance, CultureInfo bindingCulture, bool useRegexMatching = true, bool useParamMatching = true, bool useScopeMatching = true)
        {
            if (useParamMatching)
                useRegexMatching = true;

            if (stepDefinitionBinding.StepDefinitionType != stepInstance.StepDefinitionType)
                return BindingMatch.NonMatching;

            Match match = null;
            if (useRegexMatching)
            {
                if (!stepDefinitionBinding.IsValid)
                    throw _errorProvider.GetInvalidStepDefinitionError(stepDefinitionBinding);
                match = stepDefinitionBinding.Regex.Match(stepInstance.Text);
                if (!match.Success)
                    return BindingMatch.NonMatching;
            }

            int scopeMatches = 0;
            if (useScopeMatching && stepDefinitionBinding.IsScoped && stepInstance.StepContext != null && !stepDefinitionBinding.BindingScope.Match(stepInstance.StepContext, out scopeMatches))
                return BindingMatch.NonMatching;

            var arguments = match == null ? Array.Empty<MatchArgument>() : _matchArgumentCalculator.CalculateArguments(match, stepInstance, stepDefinitionBinding);

            if (useParamMatching)
            {
                Debug.Assert(match != null); // useParamMatching -> useRegexMatching
                var bindingParameters = stepDefinitionBinding.Method.Parameters.ToArray();

                // check if the regex + extra arguments match to the binding method parameters
                if (arguments.Length != bindingParameters.Length)
                    return BindingMatch.NonMatching;

                // Check if regex & extra arguments can be converted to the method parameters
                //if (arguments.Zip(bindingParameters, (arg, parameter) => CanConvertArg(arg, parameter.Type)).Any(canConvert => !canConvert))
                if (arguments.Where((arg, argIndex) => !CanConvertArg(arg.Value, bindingParameters[argIndex].Type, bindingCulture)).Any())
                    return BindingMatch.NonMatching;
            }

            return new BindingMatch(stepDefinitionBinding, scopeMatches, arguments, stepInstance.StepContext);
        }

        public BindingMatch GetBestMatch(StepInstance stepInstance, CultureInfo bindingCulture, out StepDefinitionAmbiguityReason ambiguityReason, out List<BindingMatch> candidatingMatches)
        {
            candidatingMatches = GetCandidatingBindingsForBestMatch(stepInstance, bindingCulture).ToList();
            KeepMaxScopeMatches(candidatingMatches);

            ambiguityReason = StepDefinitionAmbiguityReason.None;
            if (candidatingMatches.Count > 1)
                ambiguityReason = StepDefinitionAmbiguityReason.AmbiguousSteps;
            else if (candidatingMatches.Count == 0)
                ambiguityReason = OnNoMatch(stepInstance, bindingCulture, out candidatingMatches);

            if (candidatingMatches.Count == 1 && ambiguityReason == StepDefinitionAmbiguityReason.None)
                return candidatingMatches[0];
            return BindingMatch.NonMatching;
        }

        protected virtual IEnumerable<BindingMatch> GetCandidatingBindingsForBestMatch(StepInstance stepInstance, CultureInfo bindingCulture)
        {
            return GetCandidatingBindings(stepInstance, bindingCulture, useParamMatching: true);
        }

        private static void KeepMaxScopeMatches(List<BindingMatch> matches)
        {
            if (matches.Count > 1)
            {
                // if there are both scoped and non-scoped matches, we take the ones with the higher degree of scope matches
                int maxScopeMatches = matches.Max(m => m.ScopeMatches);
                matches.RemoveAll(m => m.ScopeMatches < maxScopeMatches);
            }
        }

        protected virtual StepDefinitionAmbiguityReason OnNoMatch(StepInstance stepInstance, CultureInfo bindingCulture, out List<BindingMatch> matches)
        {
            /*
            //HACK: since out param matching does not support argument converters yet, we rather show more results than "no match"
            matches = GetCandidatingBindings(stepInstance, useParamMatching: false).ToList();
             */

            // there were either no regex match or it was filtered out by the param/scope matching
            // to provide better error message for the param matching error, we re-run
            // the matching without scope and param check

            var matchesWithoutScopeCheck = GetCandidatingBindings(stepInstance, bindingCulture, useScopeMatching: false).ToList();

            if (matchesWithoutScopeCheck.Count > 0)
            {
                matches = matchesWithoutScopeCheck;
                return StepDefinitionAmbiguityReason.AmbiguousScopes;
            }

            Debug.Assert(matchesWithoutScopeCheck.Count == 0);

            var matchesWithoutParamCheck = GetCandidatingBindings(stepInstance, bindingCulture, useParamMatching: false).ToList();
            matches = matchesWithoutParamCheck;

            if (matchesWithoutParamCheck.Count == 1)
            {
                // no ambiguity, but param error -> execute will find it out
                return StepDefinitionAmbiguityReason.None;
            }
            if (matchesWithoutParamCheck.Count > 1) // ambiguity, because of param error
                return StepDefinitionAmbiguityReason.ParameterErrors;

            return StepDefinitionAmbiguityReason.None; // no ambiguity: simple missing step definition
        }

        protected IEnumerable<BindingMatch> GetCandidatingBindings(StepInstance stepInstance, CultureInfo bindingCulture, bool useRegexMatching = true, bool useParamMatching = true, bool useScopeMatching = true)
        {
            var matches = _bindingRegistry.GetConsideredStepDefinitions(stepInstance.StepDefinitionType, stepInstance.Text).Select(b => Match(b, stepInstance, bindingCulture, useRegexMatching, useParamMatching, useScopeMatching)).Where(b => b.Success);
            // we remove duplicate matches for the same method (take the highest scope matches from each)
            matches = matches.GroupBy(m => m.StepBinding.Method, (_, methodMatches) => methodMatches.OrderByDescending(m => m.ScopeMatches).First(), BindingMethodComparer.Instance);
            return matches;
        }
    }
}
